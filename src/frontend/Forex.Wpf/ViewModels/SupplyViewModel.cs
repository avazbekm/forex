namespace Forex.Wpf.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Forex.ClientService.Enums;
using Forex.Wpf.Pages.Common;

public partial class SupplyViewModel : ViewModelBase
{
    [ObservableProperty] private long id;
    [ObservableProperty] private DateTime date;
    [ObservableProperty] private SupplyPartyType partyType;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private string? description;
    [ObservableProperty] private UserViewModel user = default!;
    [ObservableProperty] private CurrencyViewModel currency = default!;

    public string PartyTypeText => PartyType == SupplyPartyType.Supplier ? "Ta'minotchi" : "Vositachi";
}
