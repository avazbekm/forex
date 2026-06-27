namespace Forex.Wpf.Pages.Sales.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Forex.ClientService.Enums;
using Forex.Wpf.ViewModels;

// Savdoga biriktiriladigan bitta to'lov qatori (mavjud yoki formada kiritilgan).
public partial class PaymentLineViewModel : ObservableObject
{
    // null = formada yangi kiritilgan; qiymat bor = mavjud (DB'dagi) to'lov.
    public long? TransactionId { get; set; }
    public bool IsExisting => TransactionId.HasValue;

    // true = ushbu savdoga allaqachon biriktirilgan (tahrirlash holatida).
    public bool IsLinkedToSale { get; set; }

    // Mavjud to'lov uchun asl ma'lumotlar (Update'da saqlab qolish uchun).
    public DateTime Date { get; set; }
    public decimal Discount { get; set; }

    // Mavjud to'lov oynada tahrirlandi — saqlashda Update qilinadi.
    public bool IsModified { get; set; }

    [ObservableProperty] private bool isSelected = true;
    [ObservableProperty] private CurrencyViewModel? currency;
    [ObservableProperty] private decimal exchangeRate = 1;
    [ObservableProperty] private PaymentMethod paymentMethod;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private string? description;

    public string CurrencyCode => Currency?.Code ?? string.Empty;

    public string MethodText => PaymentMethod switch
    {
        PaymentMethod.Naqd => "Naqd",
        PaymentMethod.Plastik => "Plastik",
        PaymentMethod.HisobRaqam => "Hisob raqam",
        PaymentMethod.MobilIlova => "Mobil ilova",
        _ => PaymentMethod.ToString()
    };

    public string OriginText => IsLinkedToSale ? "Biriktirilgan" : IsExisting ? "Mavjud" : "Yangi";

    partial void OnCurrencyChanged(CurrencyViewModel? value) => OnPropertyChanged(nameof(CurrencyCode));
    partial void OnPaymentMethodChanged(PaymentMethod value) => OnPropertyChanged(nameof(MethodText));
}
