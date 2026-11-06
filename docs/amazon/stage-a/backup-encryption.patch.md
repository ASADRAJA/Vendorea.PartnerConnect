# Stage A — proposed `backup.sh` change

**Status: PREPARED, NOT APPLIED.** This file lives in the Merchant360 repository
(`deploy/droplet/backup.sh`). Applying it is a gated operational change.

Per Decision 2 the **complete set** is encrypted — dumps as well as env — because the
database dumps themselves contain the plaintext SPR credentials.

## Proposed diff

```diff
 BACKUP_ROOT=/var/backups/merchant360
 RETAIN_DAYS=14
 ENV_DIR=/etc/merchant360/env
 DATABASES=(merchant360 partnerconnect)
+
+# Public key only. The droplet can encrypt and cannot decrypt: the private key lives in the
+# password manager with the custodians. A stolen backup set is then ciphertext with no key
+# anywhere near it — which is the whole point, since the set also carries the key that
+# protects the database contents.
+RECIPIENT=/etc/merchant360/backup-recipient.pub
+
+command -v age >/dev/null || { echo "age is not installed; refusing to write an unencrypted backup"; exit 1; }
+[[ -r "$RECIPIENT" ]] || { echo "recipient public key missing at $RECIPIENT"; exit 1; }

 STAMP="$(date -u +%Y%m%d-%H%M%S)"
 DEST="$BACKUP_ROOT/$STAMP"
 mkdir -p "$DEST"
+chmod 700 "$DEST"

 for db in "${DATABASES[@]}"; do
-    out="$DEST/$db.dump"
-    sudo -u postgres pg_dump --format=custom --compress=6 "$db" > "$out" \
-        || fail "pg_dump $db"
-    pg_restore --list "$out" >/dev/null 2>&1 \
-        || fail "$db dump is unreadable"
+    plain="$DEST/$db.dump"
+    out="$plain.age"
+
+    # Verify the dump BEFORE encrypting: pg_restore --list cannot read ciphertext, and an
+    # unverified backup is the failure mode this script was written to avoid.
+    sudo -u postgres pg_dump --format=custom --compress=6 "$db" > "$plain" \
+        || fail "pg_dump $db"
+    pg_restore --list "$plain" >/dev/null 2>&1 \
+        || fail "$db dump is unreadable"
+
+    age -R "$RECIPIENT" -o "$out" "$plain" || fail "encrypt $db"
+    shred -u "$plain" 2>/dev/null || rm -f "$plain"
+    chmod 600 "$out"
 done

-tar -czf "$DEST/env.tar.gz" -C "$(dirname "$ENV_DIR")" "$(basename "$ENV_DIR")" 2>/dev/null \
-    || fail "env files"
-chmod 600 "$DEST/env.tar.gz"
+tar -czf - -C "$(dirname "$ENV_DIR")" "$(basename "$ENV_DIR")" 2>/dev/null \
+    | age -R "$RECIPIENT" -o "$DEST/env.tar.gz.age" \
+    || fail "env files"
+chmod 600 "$DEST/env.tar.gz.age"
```

`uploads.tar.gz` and `config.tar.gz` stay in clear: product images and unit files are not
secrets, and encrypting them removes the ability to inspect a backup without a custodian.

`SHA256SUMS` is computed over the **encrypted** artefacts, so integrity can be checked
without decrypting.

## Why verify before encrypting

`pg_restore --list` is the existing script's guard against a truncated dump, and it cannot
read an `age` file. Dumping, verifying, then encrypting keeps that guard. Encrypting first
would trade a real integrity check for an unverifiable blob.

## Restore

```bash
# On a trusted host, with the custodian's private key:
age -d -i ~/.age/pc-backup-recovery.key \
    -o partnerconnect.dump  /path/to/partnerconnect.dump.age

sudo -u postgres createdb restore_test
sudo -u postgres pg_restore -d restore_test --no-owner --no-privileges < partnerconnect.dump
sudo -u postgres psql -d restore_test -c 'SELECT count(*) FROM "Orders"'
sudo -u postgres dropdb restore_test
shred -u partnerconnect.dump
```

## Verification before relying on it

| # | Check | Expected |
|---|---|---|
| A-V1 | `age -d` without the private key | Fails |
| A-V2 | `tar -tzf env.tar.gz.age` on the droplet | Fails — not a tarball |
| A-V3 | `age -d` with the private key, **off-host** | Succeeds |
| A-V4 | `pg_restore --list` on the decrypted dump | Lists tables |
| A-V5 | Restore into a scratch database off-host | Row counts match the manifest |
| A-V6 | Permissions | Sets 0700, artefacts 0600 |
| A-V7 | Missing `age` binary | Script exits non-zero; no unencrypted backup written |

**A-V3 and A-V5 are the off-host restore proof** the review requires before the new format
is relied upon. Until both pass, keep the current régime running.
