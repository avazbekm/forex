namespace Forex.ClientService;

using Forex.ClientService.Interfaces;

public class ForexClient(
    IApiAuth auth,
    IApiUser users,
    IApiUserAccount userAccounts,
    IApiSemiProducts semiProduct,
    IApiProductTypes productType,
    IApiCurrency currency,
    IApiUnitMeasures unitMeasure,
    IApiProducts products,
    IApiProductEntries productEntries,
    IApiSales sales,
    IApiTransactions transactions,
    IApiOperationRecord operationRecord,
    IApiShops shops,
    IApiProductResidues productResidues,
    IApiSupplies supplies,
    IFileStorageClient fileStorage)
{
    public IApiAuth Auth { get; } = auth;
    public IApiUser Users { get; } = users;
    public IApiOperationRecord OperationRecords { get; } = operationRecord;
    public IApiUserAccount UserAccounts { get; } = userAccounts;
    public IApiSemiProducts SemiProduct { get; } = semiProduct;
    public IApiCurrency Currencies { get; } = currency;
    public IApiUnitMeasures UnitMeasures { get; } = unitMeasure;
    public IApiProducts Products { get; } = products;
    public IApiProductTypes ProductTypes { get; } = productType;
    public IApiProductEntries ProductEntries { get; } = productEntries;
    public IApiSales Sales { get; } = sales;
    public IApiTransactions Transactions { get; set; } = transactions;
    public IApiShops Shops { get; set; } = shops;
    public IApiProductResidues ProductResidues { get; set; } = productResidues;
    public IApiSupplies Supplies { get; set; } = supplies;
    public IFileStorageClient FileStorage { get; } = fileStorage;
}
