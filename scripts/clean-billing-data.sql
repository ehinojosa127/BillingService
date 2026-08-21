-- Limpia datos transaccionales de facturación.
-- Conserva: emisor (issuers), series (document_series), plantillas PDF (pdf_templates).

BEGIN;

TRUNCATE TABLE audit_logs RESTART IDENTITY;
TRUNCATE TABLE idempotency_records RESTART IDENTITY;
TRUNCATE TABLE documents RESTART IDENTITY CASCADE;

UPDATE document_series
SET last_number = 0
WHERE document_type_code <> 'RA';

COMMIT;
