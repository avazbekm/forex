-- =============================================================================
-- Forex — Barkod tizimi (ProductType barkodlari + pachka + qaytarish restock)
-- =============================================================================
-- ProductTypes:
--   "QopBarcode"      — qop (sack) barkodi (noyob, NULL bo'lishi mumkin)
--   "PachkaBarcode"   — pachka (to'plam) barkodi (noyob, NULL bo'lishi mumkin)
--   "PachkaItemCount" — 1 pachkadagi juftlar soni (5/6)
--
-- ReturnItems:
--   "RestockCount"    — qaytgan mahsulotning FAQAT qopda kelgan, omborga qaytadigan
--                       juftlar soni. Pachka/dona qaytsa faqat narxi hisoblanadi
--                       (kredit), omborga qaytmaydi (qadoqlash bo'limiga olinadi).
--
-- Ishlatish: bir marta ishga tushiring (idempotent — qayta ishga tushsa xavfsiz).
-- Mavjud turlarga barkodni dasturdagi "Barcha barkodlarni yaratish" amali to'ldiradi.
-- =============================================================================

BEGIN;

ALTER TABLE "ProductTypes" ADD COLUMN IF NOT EXISTS "QopBarcode" text;
ALTER TABLE "ProductTypes" ADD COLUMN IF NOT EXISTS "PachkaBarcode" text;
ALTER TABLE "ProductTypes" ADD COLUMN IF NOT EXISTS "PachkaItemCount" integer NOT NULL DEFAULT 0;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductTypes_QopBarcode"
    ON "ProductTypes" ("QopBarcode") WHERE "QopBarcode" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductTypes_PachkaBarcode"
    ON "ProductTypes" ("PachkaBarcode") WHERE "PachkaBarcode" IS NOT NULL;

ALTER TABLE "ReturnItems" ADD COLUMN IF NOT EXISTS "RestockCount" integer NOT NULL DEFAULT 0;

COMMIT;
