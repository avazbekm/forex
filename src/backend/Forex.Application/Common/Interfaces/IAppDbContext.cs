namespace Forex.Application.Common.Interfaces;

using Forex.Domain.Entities;
using Forex.Domain.Entities.Products;
using Forex.Domain.Entities.Sales;
using Forex.Domain.Entities.SemiProducts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Account> Accounts { get; set; }
    DbSet<UserAccount> UserAccounts { get; set; }
    DbSet<ShopAccount> ShopCashAccounts { get; set; }
    DbSet<Currency> Currencies { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductResidue> ProductResidues { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<ProductEntry> ProductEntries { get; }
    DbSet<ProductResidue> ResidueShops { get; }
    DbSet<UnitMeasure> UnitMeasures { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<Return> Returns { get; set; }
    DbSet<ReturnItem> ReturnItems { get; set; }
    DbSet<SemiProduct> SemiProducts { get; }
    DbSet<Shop> Shops { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<UserNotification> UserNotifications { get; set; }
    DbSet<CompanyInfo> CompanyInfo { get; set; }
    DbSet<SocialLink> SocialLinks { get; set; }
    DbSet<OperationRecord> OperationRecords { get; set; }
    DbSet<Supply> Supplies { get; set; }

    Task<bool> SaveAsync(CancellationToken cancellation);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<bool> CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
