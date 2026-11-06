#!/usr/bin/env python3
"""Contract package validator. Run from docs/amazon:  python3 validate_contracts.py
Exits non-zero on any failure. No third-party dependencies required for the JSON checks;
YAML parsing falls back to `ruby -ryaml` when PyYAML is absent."""
import json, re, subprocess, sys, hmac, hashlib
from pathlib import Path

FAIL, PASS = [], []
def check(name, ok, detail=""):
    (PASS if ok else FAIL).append(name)
    print(f"  {'PASS' if ok else 'FAIL'}  {name}{(' — ' + detail) if detail and not ok else ''}")

def load_yaml(path):
    try:
        import yaml
        return yaml.safe_load(Path(path).read_text())
    except ImportError:
        out = subprocess.run(
            ["ruby", "-ryaml", "-rjson", "-e",
             f'puts YAML.safe_load(File.read("{path}"), aliases: true).to_json'],
            capture_output=True, text=True)
        if out.returncode != 0:
            raise RuntimeError(f"cannot parse {path}: {out.stderr.strip()}")
        return json.loads(out.stdout)

print("\n1. Specifications parse")
pc   = load_yaml("partnerconnect-amazon-api-v1.openapi.yaml");  check("PC OpenAPI parses", True)
m360 = load_yaml("merchant360-partnerconnect-api-v1.openapi.yaml"); check("M360 OpenAPI parses", True)
ev   = json.loads(Path("partnerconnect-m360-events-v1.schema.json").read_text()); check("Event schema parses", True)

print("\n2. Versions aligned at v1.1")
for n, d in (("PC", pc), ("M360", m360)):
    check(f"{n} info.version is 1.1.x", d["info"]["version"].startswith("1.1"), d["info"]["version"])
check("Index declares v1.1", "**Version:** v1.1" in Path("PARTNERCONNECT_AMAZON_CONTRACT_INDEX.md").read_text())

print("\n3. Every operation has responses and a summary")
for name, spec in (("PC", pc), ("M360", m360)):
    for route, ops in spec["paths"].items():
        for verb, op in ops.items():
            if verb == "parameters": continue
            check(f"{name} {verb.upper()} {route} has responses", bool(op.get("responses")))
            check(f"{name} {verb.upper()} {route} has summary", bool(op.get("summary")))

print("\n4. $ref targets resolve (local refs)")
def refs(node, out):
    if isinstance(node, dict):
        for k, v in node.items():
            if k == "$ref" and isinstance(v, str): out.append(v)
            else: refs(v, out)
    elif isinstance(node, list):
        for i in node: refs(i, out)
for name, spec in (("PC", pc), ("M360", m360)):
    found = []; refs(spec, found)
    for r in found:
        if not r.startswith("#/"): continue
        node, ok = spec, True
        for part in r[2:].split("/"):
            part = part.replace("~1", "/")
            if isinstance(node, dict) and part in node: node = node[part]
            else: ok = False; break
        check(f"{name} ref {r}", ok, "unresolved")

print("\n5. Event schema integrity")
defs = ev["$defs"]
check("Envelope requires eventId/eventType/payload",
      {"eventId","eventType","payload"} <= set(defs["Envelope"]["required"]))
types = set(defs["EventType"]["enum"])
check("5 event types declared", len(types) == 5, str(len(types)))
conds = [c["if"]["properties"]["eventType"]["const"] for c in defs["Envelope"]["allOf"]]
check("Every event type has a payload binding", set(conds) == types,
      f"unbound: {types - set(conds)}")
check("publicationReason present (C6)", "publicationReason" in defs["Envelope"]["properties"])
check("Money is string-encoded (no float)",
      defs["Money"]["properties"]["amount"]["type"] == "string")

print("\n6. Examples validate against the schema")
md = Path("AMAZON_CONTRACT_EXAMPLES.md").read_text()
blocks = re.findall(r"```json\n(.*?)```", md, re.S)
envelopes = 0
for i, b in enumerate(blocks, 1):
    try:
        obj = json.loads(b)
    except Exception as e:
        check(f"example {i} is JSON", False, str(e)); continue
    check(f"example {i} is JSON", True)
    if "eventType" in obj:
        envelopes += 1
        miss = [k for k in defs["Envelope"]["required"] if k not in obj]
        check(f"example {i} envelope complete", not miss, f"missing {miss}")
        check(f"example {i} eventType known", obj["eventType"] in types)
        for mk in ("orderTotal","itemPrice"):
            for m in re.findall(rf'"{mk}":\s*{{[^}}]*"amount":\s*("[^"]*"|[0-9.]+)', b):
                check(f"example {i} {mk} amount is a string", m.startswith('"'), m)
check("at least two envelope examples", envelopes >= 2, str(envelopes))

print("\n7. Cross-document consistency")
comp = Path("AMAZON_CONTRACT_COMPATIBILITY_AND_SECURITY.md").read_text()
idx  = Path("PARTNERCONNECT_AMAZON_CONTRACT_INDEX.md").read_text()
check("Signature covers timestamp + body (C1)", 'timestamp + "." + ' in comp or 'timestamp and the raw body' in comp)
check("KeyId rotation header documented (C1)", "X-PC-Signature-KeyId" in comp)
check("M360 spec declares the KeyId header (C1)", "X-PC-Signature-KeyId" in Path("merchant360-partnerconnect-api-v1.openapi.yaml").read_text())
check("Three auth models documented (C2)", "Three distinct authentication models" in comp)
check("M360 user auth scheme exists (C2)", "merchant360UserAuth" in m360["components"]["securitySchemes"])
check("PC service-to-service scheme exists (C4)", "serviceToService" in pc["components"]["securitySchemes"])
check("Reconnection identity documented (C3)", "Connection identity across reconnection" in comp)
check("confirmShipment pinned to v0 (C5)", "confirmShipment" in comp and "v0" in comp)
check("Retry vs republication documented (C6)", "retry vs republication" in comp.lower())
check("Index lists the six package files",
      all(f in idx for f in ["partnerconnect-amazon-api-v1.openapi.yaml",
                             "partnerconnect-m360-events-v1.schema.json",
                             "merchant360-partnerconnect-api-v1.openapi.yaml",
                             "AMAZON_CONTRACT_EXAMPLES.md",
                             "AMAZON_CONTRACT_COMPATIBILITY_AND_SECURITY.md"]))
check("No Amazon SDK types leak into the contracts",
      not re.search(r"\b(com\.amazon|software\.amazon|SellingPartnerApi[A-Za-z]*Client)\b",
                    comp + json.dumps(ev) + json.dumps(pc)))

print("\n8. Signature test vector (must match Merchant360's implementation)")
secret, ts = b"pc-test-secret", "2026-11-05T14:02:13Z"
body = '{"eventId":"7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30"}'
sig = hmac.new(secret, (ts + "." + body).encode(), hashlib.sha256).hexdigest()
print(f"       v1={sig}")
bad = hmac.new(secret, (ts + "." + body.replace("7f1c", "0000")).encode(), hashlib.sha256).hexdigest()
check("tampered body yields a different signature", sig != bad)
bad_ts = hmac.new(secret, ("2026-11-05T14:30:00Z." + body).encode(), hashlib.sha256).hexdigest()
check("tampered timestamp yields a different signature (the v1.0 defect)", sig != bad_ts)

print(f"\n{'='*58}\n  {len(PASS)} passed, {len(FAIL)} failed")
if FAIL:
    print("  Failures:"); [print("    -", f) for f in FAIL]
print("="*58)
sys.exit(1 if FAIL else 0)
