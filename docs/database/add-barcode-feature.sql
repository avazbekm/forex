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
--                       Mavjud (eski) qaytarishlar to'liq "TotalCount" bilan
--                       omborga kirgan edi — shuning uchun ular uchun
--                       "RestockCount" = "TotalCount" qilib to'ldiriladi
--                       (tahrirlashda ombor qoldig'i to'g'ri tiklanishi uchun).
--
-- Ishlatish: bir marta ishga tushiring (idempotent — qayta ishga tushsa xavfsiz).
-- Mavjud turlarga barkodni dasturdagi "Barcha barkodlarni yaratish" amali to'ldiradi.
-- =============================================================================

BEGIN;

ALTER TABLE "ProductTypes" ADD COLUMN IF NOT EXISTS "QopBarcode" text;
ALTER TABLE "ProductTypes" ADD COLUMN IF NOT EXISTS "PachkaBarcode" text;
ALTER TABLE "ProductTypes" ADD COLUMN IF NOT EXISTS "PachkaItemCount" integer NOT NULL DEFAULT 0;

DROP INDEX IF EXISTS "IX_ProductTypes_QopBarcode";
CREATE UNIQUE INDEX "IX_ProductTypes_QopBarcode"
    ON "ProductTypes" ("QopBarcode") WHERE "QopBarcode" IS NOT NULL AND "QopBarcode" <> '';

DROP INDEX IF EXISTS "IX_ProductTypes_PachkaBarcode";
CREATE UNIQUE INDEX "IX_ProductTypes_PachkaBarcode"
    ON "ProductTypes" ("PachkaBarcode") WHERE "PachkaBarcode" IS NOT NULL AND "PachkaBarcode" <> '';

ALTER TABLE "ReturnItems" ADD COLUMN IF NOT EXISTS "RestockCount" integer;
UPDATE "ReturnItems" SET "RestockCount" = "TotalCount" WHERE "RestockCount" IS NULL;
ALTER TABLE "ReturnItems" ALTER COLUMN "RestockCount" SET DEFAULT 0;
ALTER TABLE "ReturnItems" ALTER COLUMN "RestockCount" SET NOT NULL;

COMMIT;
