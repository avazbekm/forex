ALTER TABLE "OperationRecords"
ADD COLUMN IF NOT EXISTS "SupplyId" bigint NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_OperationRecords_Supplies_SupplyId'
    ) THEN
        ALTER TABLE "OperationRecords"
        ADD CONSTRAINT "FK_OperationRecords_Supplies_SupplyId"
        FOREIGN KEY ("SupplyId") REFERENCES "Supplies" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_OperationRecords_SupplyId" ON "OperationRecords" ("SupplyId");
