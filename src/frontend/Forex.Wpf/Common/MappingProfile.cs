namespace Forex.Wpf.Common;

using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Forex.Wpf.ViewModels;
using Mapster;

public static class MappingProfile
{
    public static void Register(TypeAdapterConfig config)
    {
        // 🔹 Product
        config.NewConfig<ProductResponse, ProductViewModel>();
        config.NewConfig<ProductViewModel, ProductViewModel>()
            .PreserveReference(true);
        config.NewConfig<ProductViewModel, ProductRequest>()
            .Map(dest => dest.UnitMeasureId, src => src.UnitMeasure.Id);

        // 🔹 ProductTypes
        config.NewConfig<ProductTypeResponse, ProductTypeViewModel>();
        config.NewConfig<ProductTypeViewModel, ProductTypeViewModel>()
            .PreserveReference(true);
        config.NewConfig<ProductTypeViewModel, ProductTypeRequest>();

        config.NewConfig<ProductEntryViewModel, ProductEntryViewModel>()
            .PreserveReference(true);

        config.NewConfig<ProductEntryViewModel, ProductEntryRequest>();
        config.NewConfig<ProductEntryResponse, ProductEntryViewModel>()
            .Map(dest => dest.BundleCount, src => src.Count / src.BundleItemCount)
            .Map(dest => dest.Date, src => src.Date.ToLocalTime());

        // 🔹 SemiProduct
        config.NewConfig<SemiProductResponse, SemiProductViewModel>();
        config.NewConfig<SemiProductViewModel, SemiProductRequest>()
            .Map(dest => dest.UnitMeasureId, src => src.UnitMeasure.Id);

        // Sale
        config.NewConfig<SaleViewModel, SaleRequest>();
        config.NewConfig<SaleResponse, SaleViewModel>()
            .Map(dest => dest.Date, src => src.Date.ToLocalTime());

        // 🔹 UnitMeasures
        config.NewConfig<UnitMeasureResponse, UnitMeasuerViewModel>();
        config.NewConfig<UnitMeasuerViewModel, UnitMeasureRequest>();

        // 🔹 Customer
        config.NewConfig<UserResponse, UserViewModel>();
        config.NewConfig<UserViewModel, UserRequest>();

        // 🔹 Currencies
        config.NewConfig<CurrencyResponse, CurrencyViewModel>();
        config.NewConfig<CurrencyViewModel, CurrencyRequest>();

        // Currency
        config.NewConfig<CurrencyResponse, CurrencyViewModel>();
        config.NewConfig<UserAccountResponse, UserAccountViewModel>();

        // Supply
        config.NewConfig<SupplyResponse, SupplyViewModel>()
            .Map(dest => dest.Date, src => src.Date.ToLocalTime());
        config.NewConfig<SupplyViewModel, SupplyRequest>()
            .Map(dest => dest.UserId, src => src.User.Id)
            .Map(dest => dest.CurrencyId, src => src.Currency.Id);

        // 🔹 Transaction
        config.NewConfig<TransactionResponse, TransactionViewModel>()
            .Map(dest => dest.Date, src => src.Date.ToLocalTime())
      .AfterMapping((src, dest) =>
      {
          if (src.IsIncome)
          {
              dest.Income = src.Amount;
              dest.Expense = 0;
          }
          else
          {
              dest.Expense = -src.Amount;
              dest.Income = 0;
          }
      });
        config.NewConfig<TransactionViewModel, TransactionRequest>()
            .Map(dest => dest.ExchangeRate, src => src.Currency.ExchangeRate)
            .Map(dest => dest.CurrencyId, src => src.Currency.Id)
            .Map(dest => dest.UserId, src => src.User.Id);
    }
}
