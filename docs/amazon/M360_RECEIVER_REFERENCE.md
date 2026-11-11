# Merchant360 Receiver — Reference Implementation Notes

**For:** whoever builds backlog item 4.6 (receiver, durable inbox, idempotency).
**Date:** 2026-11-11
**Working code:** `tests/Vendorea.PartnerConnect.DatabaseTests/ReferenceConsumerReceiver.cs`

That file is a complete, independently written receiver. It verifies signatures with its own
HMAC implementation rather than calling PartnerConnect's, which is deliberate: the end-to-end
loop test then proves two independent implementations agree, instead of proving PartnerConnect
agrees with itself. Read it alongside this page — it is short, and the reasoning is in the
comments.

---

## 1. The verification ladder, in order

The order is the point. Each step assumes the previous one held, and moving any of them changes
what the endpoint is willing to do on behalf of an unauthenticated caller.

| # | Check | Failure |
|---|---|---|
| 1 | All three headers present | 401 |
| 2 | `X-PC-Signature-Timestamp` within ±5 minutes of now | 401 |
| 3 | `X-PC-Signature-KeyId` resolves to a known secret | 401 |
| 4 | Recomputed HMAC equals `X-PC-Signature`, compared in constant time | 401 |
| 5 | **Only now** deserialise the body | 400 if unparseable |

Four things that are easy to get wrong:

- **Reject a stale timestamp; never clamp it.** Clamping accepts a replay of a capture from last
  week, which is precisely what binding the timestamp into the signature was introduced to stop.
- **An unknown key id is a 401, not a fallback to a default key.** A fallback is how a retired
  secret keeps working for ever.
- **Verify against the raw bytes as received.** Deserialising and re-serialising to verify will
  reject every valid delivery you are ever sent — whitespace and key order change. There is a
  test demonstrating exactly this (`A_body_re_serialised_in_transit_fails_even_though_it_means_the_same_thing`).
- **Use a constant-time comparison.** `CryptographicOperations.FixedTimeEquals`, or your
  platform's equivalent. A plain string compare returns on the first differing byte and leaks how
  much of a guessed signature was right.

## 2. The signature

```
signingString = <X-PC-Signature-Timestamp> + "." + <raw request body bytes>
signature     = "v1=" + lowercase_hex( HMAC_SHA256( secret, utf8(signingString) ) )
```

### Check your implementation against this vector before anything else

```
secret    = "pc-test-secret"
timestamp = "2026-11-05T14:02:13Z"
body      = {"eventId":"7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30"}
expected  = v1=a050819d48d6e1cf3796d4b825f39b856ceb17d551e435ad906747a354eedd0c
```

PartnerConnect pins this in `IntegrationSignatureTests` and the reference receiver pins it again
independently. **Pin it in the Merchant360 repository too.** It is the one check that catches a
divergence in CI rather than in production, and it costs one test.

### Rotation

Accept **several** key ids at once. PartnerConnect signs with its primary key and sends that id;
you accept any id you still hold. To rotate: add the new secret on your side first, have
PartnerConnect switch its primary, then retire the old id. No redeploy, no delivery gap. There is
a loop test for the overlap (`A_rotation_overlap_lets_a_new_key_sign_while_the_old_one_still_verifies`).

## 3. The inbox

Delivery is **at-least-once**. Business effects must be **effective-once**, and that is the
consumer's obligation, not the publisher's.

- Key the inbox on **`eventId`**. A transport retry reuses it, so an event already present is
  acknowledged without being processed again.
- Key the **order** on the identity tuple — `payload.identity.provider` +
  `integrationConnectionId` + `externalOrderId`. Exactly one Merchant360 sales order per tuple,
  for the life of the order.
- **`eventId` never governs order identity.** A manual redelivery or a reconciliation
  republication arrives with a *new* `eventId` carrying the *same* identity, and must **update**
  the order rather than create a second one. `publicationReason` tells you which it is.
- `deduplicationKey` is immutable per state transition: equal across retries of one transition,
  different across genuine transitions. Use it to tell "I have seen this change" from "this is a
  new change".

## 4. What 202 means, and what it does not

**A 2xx means the bytes arrived and are in your inbox. Nothing more.**

PartnerConnect records it as `DeliveryAcknowledged` and explicitly not as a business outcome. When
your handler has actually finished, call back:

```
POST /api/v1/org/tenants/{tenantId}/sales-orders/{pcSalesOrderId}/processing-result
{ "sourceEventId": "...", "outcome": "Processed", "m360SalesOrderId": "...", "attemptedAt": "..." }
```

`outcome` is `Processed`, `FailedRetryable` or `FailedPermanent`. Authenticate with your
organization API key — **not** the webhook signing secret, which authorises nothing inbound to
PartnerConnect.

What PartnerConnect does with it:

| Situation | Response |
|---|---|
| First result | `204` |
| Same result again | `200`, original retained, timestamp unchanged |
| An event never published for that order | `409` |
| A *different* result for the same event | `409`, **first result wins** |

That last row matters: two different answers for one event means your handler ran twice with
different effects. PartnerConnect refuses to overwrite rather than hide it. A different
`m360SalesOrderId` for the same outcome is also a conflict — that is two Merchant360 orders from
one event, the duplicate the whole exchange exists to prevent.

**Until you call this, PartnerConnect does not know whether the order exists in Merchant360.**
Skipping it is how an order silently never appears for a merchant.

## 5. Envelope fields you must not ignore

| Field | Why |
|---|---|
| `payload.status` = `Unknown` | Amazon sent a status we do not map. Surface it for operator attention; **never treat it as progress** |
| `payload.mappingStatus` = `UnmappedProduct` | Still create a visible order line so somebody can resolve it. Never discard the line, never invent a product |
| `payload.lines[].m360CatalogItemIdHint` | A **hint**. Merchant360 owns product identity and must verify it — a mapping taken on faith decrements the wrong stock |
| `payload.buyerDataAvailable` = `false` | Normal for `PENDING` orders and for any order fetched before the restricted-data role was granted. Not an error, not an alert |
| `payload.orderTotal` absent | Means the PROCEEDS role was not granted. Not a free order |
| Money fields | Decimal **strings**. Parse as decimal, never as a float |
| `programsRaw` | Derive Prime/Business from it. No `isPrime` boolean crosses the wire, because a boolean per programme answers "false" for every programme added later |

## 6. Buyer data

`IncludeBuyerData` is **off** in PartnerConnect by default, so `buyer` and `shipTo` are omitted
until it is deliberately switched on. A merchant-fulfilled order cannot be shipped without the
address, so it will be switched on — but it is a decision about sending personal data between
systems, not a default.

A PartnerConnect order whose buyer data has been redacted under retention is **never** republished
with the address restored, so a redelivery cannot resurrect personal data Merchant360 had also
disposed of.

## 7. Tests worth copying

The loop tests in `Merchant360ExchangeLoopTests.cs` are written from PartnerConnect's side, but
each has a mirror image on yours:

| PartnerConnect test | Your equivalent |
|---|---|
| Reproduces the published signature vector | **Do this first** |
| Body altered in transit → 401 | Tamper a body, assert rejection |
| Body re-serialised in transit → 401 | Verify against raw bytes, not a round trip |
| Unknown key id → 401 | No default-key fallback |
| Rotation overlap accepted | Two keys valid at once |
| Same event delivered twice → one effect | Your inbox dedupe |
| Redelivery → new `eventId`, same identity, updates the order | **The one that matters most** |
| Status change → different `deduplicationKey`, not deduped away | Otherwise you silently drop shipments |
