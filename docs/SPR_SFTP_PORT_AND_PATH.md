# SPR SFTP: the port, and the path behind it

2026-11-19. The SPR price-feed poller had failed on every cycle for 86 days — 21,802 batch rows,
each `Connection failed to establish within 30000 milliseconds`. The port was the presenting
cause. It was not the only one.

## What was wrong

**The port.** `SprConfiguration.SftpPort` defaulted to `22`, and the deployed
`TradingPartners.TransportConfigJson` did not set it at all, so the default applied.
`gist.sprich.com` does not answer on 22; it answers on **50022**:

```
port 22: no answer
SSH-2.0-SFTP Server   <= port 50022
```

What makes this worth writing down is that the right port was already in the codebase. When the
XML order exchange was found to need 50022, it was introduced as `SprXmlSftpPort` and deliberately
scoped to that one flow, with a comment explaining that this left "other SPR SFTP flows
(price/inventory feeds) unaffected."

They were not unaffected. Both flows build their connection from the same `SftpHost`, the same
`SftpUsername` and the same password — there is one SFTP server here, not two. Scoping the
non-standard port to one flow did not protect the other; it left it permanently unable to connect.

The unit test made this durable rather than catching it. `SprConfiguration_XmlExchangeDefaultsTo50022_OtherFlowsUnchanged`
asserted `SftpPort.Should().Be(22)` and passed, every run, for the whole 86 days. It pinned the
defect in place and reported it as correctness.

**The path.** Fixing the port did not make the feed work — it changed the failure:

```
Price feed failed for partner 1: User [FTPGOF] has no view rights to [/sp/edi/cust/FTPGOF/outbound/prices]
```

That is progress, not a setback: a 30-second timeout became a server response, which is how we
know the port fix landed. `PriceFeedPath` defaulted to `/outbound/prices`, a path that does not
exist. The account is chrooted, and it contains exactly two directories:

```
sftp> pwd      → /
drwx------  in
drwx------  out
```

`/outbound/prices` was never anything but a plausible-looking invention. The XML flow had already
been configured against reality — `./in` and `./out` — which is the second signal that was present
and unused.

## What was changed

| Where | Change |
|---|---|
| `SprConfiguration.SftpPort` | default `22` → `50022`, with the reasoning above kept in the XML doc comment |
| `SprEzpo4GeneratorTests` | the test that pinned `22` now asserts both flows default to `50022`, and says why; a second test covers the per-partner override |
| `TradingPartners.TransportConfigJson` (droplet) | `"SftpPort": 50022` added explicitly, so the config does not depend on the default |
| `TradingPartners.TransportConfigJson` (droplet) | `"PriceFeedPath": "./out"` added |

`./out` is where SPR delivers to on this account and where the XML inbound already points. The
`*.csv` price pattern and the `*.xml` XML pattern do not overlap, so the two flows can share it.

Both config changes were made with `jsonb_set`, which left every other key untouched;
`TransportCredentialsJson` was not read or written. The row was dumped to
`/root/tradingpartners-backup-20261119-045404.sql` first.

## Verified

Against the live server, through the application's own code path — not by inspection:

```
[05:00:23 INF] No price feed files found for dealer 1
PriceFeedBatches before: 22344
PriceFeedBatches after:  22344   (delta 0)
```

The poll now **connects, authenticates, lists the directory and finds it empty**. Because an empty
readable directory returns `PriceFeedResult(true, null, 0, null)`, the batch comes back
`Completed` with `TotalItems = 0` and no document, which is exactly the `RecordsNoFile` condition
— so the worker records nothing at all.

Two consequences:

- **The 86-day growth has stopped.** 22,344 rows remain as the record of the outage and are worth
  keeping; a `DELETE` on `TotalItems = 0` alone would have destroyed that evidence, which is why
  the earlier one was written to match `Completed` and correctly hit 0 rows.
- **The `RecordsNoFile` guard now does the job it was written for.** It never suppressed anything
  before, because the batches were `Failed`, not `Completed`. It needed a working connection to
  become meaningful.

## What this does *not* mean

**The automated price feed is not working. It has no source.**

Prices reach the system today by **manual upload**, and only that way:

| Id | File | Status | Uploaded |
|---|---|---|---|
| 4 | `003382200-BESTPRICE-CSVFull 2.csv` | PushedToMerchant360 | 2026-10-27 |
| 3 | `003382200-BESTPRICE-CSVFull_AUGUST_2026.csv` | PushedToMerchant360 | 2026-08-29 |
| 2 | `003382200-BESTPRICE-CSVFull_AUGUST_2026.csv` | PushedToMerchant360 | 2026-08-24 |
| 1 | `003382200-BESTPRICE-CSVFull_AUGUST_2026.csv` | Failed | 2026-08-24 |

The `FTPGOF` account looks provisioned for the XML order exchange — `in` for outbound EZPO4, `out`
for POACK/ASN/invoice (2 `SprXmlDocuments` exist, 0 `EdiDocuments`). Both directories are empty
right now, and no price file has ever been observed in either.

So `./out` is the best-supported setting available, not a confirmed one. It is where files would
arrive if SPR delivers them to this account at all.

## Open with SPR

1. **Does SPR deliver the price file to SFTP for this account, or is the portal download the only
   channel?** If SFTP, which directory and what filename pattern? Today's manual files are
   `003382200-BESTPRICE-CSVFull*.csv`, which would not match the configured `*.csv`… it would,
   but the account number in the name suggests a per-account path worth confirming.
2. **Is `gist.sprich.com:50022` the only SFTP endpoint**, or is there a separate host for feeds?
   Port 22 not answering suggests one endpoint.
3. Still outstanding from before: PCSTC level 1/2/3 distinction, price-code expiry, whether `.00`
   is an account or the group, and **test-only SFTP credentials** (this account's password sits in
   plaintext in every retained backup).

## Still broken underneath this

Even with a file delivered, **the automated SFTP path would store no prices.** It parses and
discards — `SprAdapter` returns only `parseResult.Items.Count`, and `FeedProcessingService`
fabricates the counts it reports. Fixing the port and path makes the transport work; it does not
make the ingestion work. That is the next piece, and it is the one that matters if SPR answers
question 1 with "yes, SFTP".
