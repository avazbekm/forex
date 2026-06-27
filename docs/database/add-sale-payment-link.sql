-- =============================================================================
-- Forex — Savdo ↔ To'lov bog'lash (Transactions."SaleId")
-- =============================================================================
-- To'lov (Transaction) qaysi savdoga (Sale) tegishli ekanini ID bilan bog'laydi.
-- Bog'lanmagan to'lovlar uchun "SaleId" = NULL.
--
-- Ishlatish:
--   Ushbu skriptni bir marta ishga tushiring (qayta ishga tushsa ham xavfsiz, idempotent).
--   ON DELETE SET NULL — savdo o'chsa, to'lov saqlanadi, faqat bog'lanishi uziladi.
-- =============================================================================

BEGIN;

ALTER TABLE "Transactions" ADD COLUMN IF NOT EXISTS "SaleId" bigint;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Transactions_Sales_SaleId') THEN
        ALTER TABLE "Transactions"
            ADD CONSTRAINT "FK_Transactions_Sales_SaleId"
            FOREIGN KEY ("SaleId") REFERENCES "Sales" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Transactions_SaleId" ON "Transactions" ("SaleId");

COMMIT;
