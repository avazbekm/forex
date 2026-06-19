ALTER TABLE "OperationRecords"
ADD COLUMN IF NOT EXISTS "UserId" bigint NULL;

UPDATE "OperationRecords" o
SET "UserId" = s."CustomerId"
FROM "Sales" s
WHERE s."OperationRecordId" = o."Id"
  AND o."UserId" IS NULL;

UPDATE "OperationRecords" o
SET "UserId" = t."UserId"
FROM "Transactions" t
WHERE t."OperationRecordId" = o."Id"
  AND o."UserId" IS NULL;

UPDATE "OperationRecords" o
SET "UserId" = sp."UserId"
FROM "Supplies" sp
WHERE o."SupplyId" = sp."Id"
  AND o."UserId" IS NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_OperationRecords_Users_UserId'
    ) THEN
        ALTER TABLE "OperationRecords"
        ADD CONSTRAINT "FK_OperationRecords_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_OperationRecords_UserId" ON "OperationRecords" ("UserId");
