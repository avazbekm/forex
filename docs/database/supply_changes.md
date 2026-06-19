# Supply database changes

This update adds financial supply tracking without changing stock residue logic.

## Required change

Run `docs/database/20260615_add_supplies.sql` on the production PostgreSQL database before deploying the updated backend.

The script adds:

- `Supplies`
- indexes for `CurrencyId`, `UserId`
- EF migration history row for `20260615163341_AddSupplies`

If earlier draft columns already exist, the script removes `Quantity`, `SemiProductId`, `ContainerCount`, `PricePerContainer`, and `ExchangeRate`.

`Supplies.PartyType` values:

- `0` - supplier
- `1` - consolidator

`Supplies` balance behavior:

- supplier supply adds `Amount` to that supplier's `Accounts.Balance`
- consolidator supply adds `Amount` to that consolidator's `Accounts.Balance`
- deleting a supply soft-deletes the supply row and subtracts the same amount back from that user's balance

## Existing tables kept

No existing table is dropped in this change. This is intentional because the live app still uses several of the tables that looked removable:

- `OperationRecords` is used by sales and transaction reporting.
- `ProductTypeItems` is still part of the product composition model.
- `Invoices`, `InvoicePayments`, `SemiProductEntries`, and `SemiProductResidues` are still referenced by the old semi-product intake code and reports.
- `ProductionBatch`, `ProductionStage`, `WorkerPayment`, and process tables are not exposed from Home anymore, but removing them should be a separate migration after confirming there is no production data or report dependency.

## Future cleanup path

If those old modules are permanently retired, do a separate cleanup release:

1. Export or archive data from the old tables.
2. Remove frontend pages and client API interfaces for processes and old semi-product intake.
3. Remove backend controllers, commands, queries, DTOs, and DbSets.
4. Generate a dedicated drop migration.
5. Test sales, payment, product, user, report, and settings flows against a copy of production data.
