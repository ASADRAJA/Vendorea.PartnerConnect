# SPR Execution Notes (Phase: ERROR ack + transport alignment)

Short operational runbook for the SPR XML exchange, reflecting SPR-confirmed rules. This
covers the alignment pass that made the outbound PO flow real and added first-class ERROR
acknowledgement handling. Items marked _(deferred)_ are intentionally out of scope for this pass.

## Transport (SFTP)

- **Protocol:** SFTP. Port **50022** (SPR's non-standard production port).
  - Configured per connection in `DealerPartnerConnection.ConfigurationJson` → `SprConfiguration.SftpPort`.
  - The SPR default is now **50022** (`SprConfiguration.SftpPort`); override per connection if needed.
- **No temp-name + rename.** Outbound files are written **directly to their final name**. SPR
  does not act on files with an open handle, but we must not use a `.tmp`→rename workflow.
  The SFTP send (`SprXmlDocumentProcessingService.SendOutboundDocumentAsync`) uploads straight
  to the final path on `SprConfiguration.SprXmlOutboundPath`.
- **No XML batch submission.** One document per file (see below).

## Outbound PO (EZPO4)

- **One PO per file.** One PartnerConnect order → one canonical `PurchaseOrder` → one `<Order>`
  EZPO4 document → one file. No batching in the SPR XML outbound path.
- **Schema conformance.** Output conforms to the real `06 SPR EZPO4 XML.XSD` (embedded at
  `Infrastructure/Schemas/SPR/EZPO4.xsd`). Generation is **strictly validated against that XSD
  before persist and again before send**; a non-conforming PO is never sent.
- **PO number field.** The dealer/PC PO number is sent as `Order/@CustomerPONo`. SPR assigns its
  own `OrderNo` / `SprSoNum` and echoes both on the POACK.
- **Dispatch action.** `POST /api/admin/orders/{id}/transmit` maps → generates → validates →
  sends and advances the order to `Processing`. This is also the manual retry path. It is
  separate from `POST /api/admin/orders/{id}/acknowledge` (an admin review step that does **not**
  imply supplier-dispatch-ready).

## Inbound POACK + ERROR acks (most important)

- A POACK is the original order **echoed back as `<Order>`** with acknowledgment data in the
  Extn extensions: line status in `EXTNSprOrderLine/@AckStatus` (+`@AckDesc`), header status in
  `EXTNSprOrderHeader/@PoAckStatus`. There is **no standalone POACK XSD**, so POACKs are parsed
  **leniently** (not strict-XSD validated).
- **One POACK per file.**
- **ERROR acknowledgements** mean **the order was NOT processed by SPR**. Two channels, one
  normalized outcome:
  1. **Structured business error** — `AckStatus = 'E'` ("order can not be processed") on a line,
     and/or an error `PoAckStatus` on the header, inside a well-formed echoed order.
  2. **Translation-level error** — the echoed order with an error message appended at the bottom,
     possibly not even well-formed XML. Handled by a tolerant fallback that extracts the PO
     number (`CustomerPONo`) and the appended message.
- For **either** channel the result is identical and never silently discarded:
  - the **raw returned document is retained** (on the `SprXmlDocument` + as the ack `RawDocument`);
  - the SPR document is marked **not-processed** (`ProcessingStatus = Failed`);
  - the order is correlated by **PO number (`CustomerPONo`)** and set to **`OrderStatus.Failed`**
    with the extracted error message;
  - a **normalized failure is surfaced to Merchant360**: `OrderStatusType.Failed`,
    `StatusCode = "SPR_ERROR_ACK"`, with the actionable message. **No raw SPR XML is sent to M360.**
- Successful POACKs move the order to `Acknowledged` and push `OrderStatusType.Acknowledged`.

## Shipments / EZASNS

- One shipment file = one shipment; one order may have multiple shipments over time. _(Multi-PO
  shipment correlation improvements are deferred.)_

## Invoices / credit memos

- Arrive as a **large batched XML file** containing many invoice/credit documents; the EZINV4
  parser splits them into individual records. _(Batched-invoice correlation hardening is deferred.)_

## Correlation

- **Primary key: PO number** (`CustomerPONo` outbound; echoed on POACK/ASN/invoice).
- **PO + Dealer Attention fallback is deferred.** The SPR-side source is
  `EXTNSprOrderHeader/@DealerAttn`; do not invent it from ship-to/bill-to names. It will be threaded
  intentionally once the Merchant360 intake source is confirmed.

## Web services _(deferred — docs only)_

- All SPR web services are available for Gemini. `DealerStockCheck` returns dealer-specific
  pricing; ZIP/drop-ship inventory lookup is available. Credentials are provisioned separately by
  the SPR helpdesk and plug into `SprConfiguration` (SOAP endpoint/username/password).

## Explicit assumptions (review before go-live)

- **`merchantId == tenantId`.** When surfacing status to Merchant360 we call
  `PushOrderStatusUpdateAsync(order.TenantId, ...)` — i.e. the PartnerConnect tenant id is used
  as the M360 merchant id. This matches existing PC→M360 usage (price feed / subscriptions). If
  M360 expects a different merchant identifier (e.g. `Tenant.ExternalId`), adjust the mapping in
  `SprXmlDocumentProcessingService.SurfaceAckToM360Async`.
- **Translation-level ERROR ack fallback is based on documented behavior, not a real sample.**
  SPR's POACK guide (Appendix A) ships only successful POACK samples; no translation-error sample
  exists yet. The channel-2 matcher (`SprPoackParser.ExtractAppendedErrorText` /
  `ExtractPoNumberLoose`) implements the documented "original order echoed + error message appended
  at the bottom" behavior and is isolated so it can be tuned the moment SPR provides a real file.

## Items waiting on live SPR test credentials / artifacts

- A real **translation-level ERROR ack sample** (Appendix A of the POACK guide has only success
  samples). The tolerant fallback matcher (`SprPoackParser.ExtractAppendedErrorText`) is built to
  documented behavior and isolated for easy tuning once a real error file is available.
- Live SPR SFTP credentials + host for an end-to-end send/receive on port 50022.
