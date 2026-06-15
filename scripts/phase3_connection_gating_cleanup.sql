-- ============================================================================
-- Phase 3 (connection-gated tenant provisioning) — DATA CLEANUP
--
-- Run this MANUALLY, per environment, AFTER the
-- 20260615030337_RetireMerchantSubscriptionRequests migration is applied.
-- It is deliberately NOT part of the auto-applied EF migration because it
-- DELETES rows and includes an environment-specific repair; each environment's
-- data must be reviewed before committing.
--
-- The script is idempotent and wrapped in a transaction so you can inspect the
-- output before COMMIT (change COMMIT to ROLLBACK to abort).
--
-- Section A: repair the split "Asad Merchant" record (dev/test data). It is a
--            no-op in any environment that does not have that exact data.
-- Section B: generic, FK-safe purge of zero-dependent "artifact" tenants left
--            behind by the retired bulk M360 mirror. Tenants that have ANY
--            business data (orders, supplier POs/invoices/credit memos) or any
--            partner connection are preserved (disable via admin UI if needed).
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- ---- Section A: repair the split "Asad Merchant" (environment-specific) -----
-- "Asad Merchant" = the tenant with ExternalId = '3' in the M360 org. Its SPR
-- connection ('ASAD-SPR-001') and its 3 orders both belong to it, but the
-- connection had been mis-attached to a different tenant ("Demo Merchant").
-- Move the connection onto Asad Merchant and normalise its org/status fields.
DECLARE @asad INT = (
    SELECT t.Id
    FROM Tenants t
    JOIN Organizations o ON o.Id = t.OrganizationId
    WHERE o.Code = 'M360' AND t.ExternalId = '3'
);

IF @asad IS NOT NULL
BEGIN
    UPDATE a
    SET a.TenantId         = @asad,
        a.OrganizationId   = (SELECT OrganizationId FROM Tenants WHERE Id = @asad),
        a.ExternalTenantId = '3',
        a.ApprovalStatus   = 'Approved',
        a.IsActive         = 1,
        a.UpdatedAt        = SYSUTCDATETIME()
    FROM TenantPartnerAccounts a
    WHERE a.AccountNumber = 'ASAD-SPR-001';
END;

-- ---- Section B: purge zero-dependent artifact tenants (generic, FK-safe) ----
DELETE t
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantPartnerAccounts a WHERE a.TenantId = t.Id)
  AND NOT EXISTS (SELECT 1 FROM Orders o                WHERE o.TenantId = t.Id)
  AND NOT EXISTS (SELECT 1 FROM SupplierPurchaseOrders s WHERE s.TenantId = t.Id)
  AND NOT EXISTS (SELECT 1 FROM SupplierInvoices s       WHERE s.TenantId = t.Id)
  AND NOT EXISTS (SELECT 1 FROM SupplierCreditMemos s    WHERE s.TenantId = t.Id);

-- ---- Review output, then COMMIT (or change to ROLLBACK to abort) ------------
SELECT
    (SELECT COUNT(*) FROM Tenants)                          AS TenantsRemaining,
    (SELECT COUNT(*) FROM TenantPartnerAccounts
        WHERE AccountNumber = 'ASAD-SPR-001' AND TenantId = @asad) AS AsadConnectionMoved;

COMMIT TRAN;
