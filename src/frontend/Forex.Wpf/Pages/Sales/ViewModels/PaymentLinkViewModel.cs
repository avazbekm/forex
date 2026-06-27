namespace Forex.Wpf.Pages.Sales.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Enums;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Requests;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using MapsterMapper;
using System.Collections.ObjectModel;

public sealed record PaymentMethodOption(PaymentMethod Value, string Text);

public sealed class AttachedPaymentLine
{
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; }
    public string MethodText { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class PaymentLinkResult
{
    public List<AttachedPaymentLine> Payments { get; init; } = [];
    public DateTime? DueDate { get; init; }
}

public partial class PaymentLinkViewModel : ViewModelBase
{
    private readonly ForexClient client;
    private readonly IMapper mapper;
    private readonly long saleId;
    private readonly long customerId;
    private readonly DateTime saleDate;
    private readonly bool isSaleEdit;
    private PaymentLineViewModel? editingLine;

    public string CustomerName { get; }
    public PaymentLinkResult? Result { get; private set; }
    public event Action<bool>? CloseRequested;

    public PaymentLinkViewModel(ForexClient client, IMapper mapper,
        long saleId, long customerId, string customerName, DateTime saleDate, bool isEditing = false)
    {
        this.client = client;
        this.mapper = mapper;
        this.saleId = saleId;
        this.customerId = customerId;
        this.saleDate = saleDate;
        this.isSaleEdit = isEditing;
        CustomerName = customerName;
        DueDate = saleDate.Date.AddDays(1);
        _ = InitAsync();
    }

    public ObservableCollection<CurrencyViewModel> Currencies { get; } = [];
    public ObservableCollection<PaymentLineViewModel> Payments { get; } = [];

    public IReadOnlyList<PaymentMethodOption> PaymentMethods { get; } =
    [
        new(PaymentMethod.Naqd, "Naqd"),
        new(PaymentMethod.Plastik, "Plastik"),
        new(PaymentMethod.HisobRaqam, "Hisob raqam"),
        new(PaymentMethod.MobilIlova, "Mobil ilova"),
    ];

    [ObservableProperty] private CurrencyViewModel? formCurrency;
    [ObservableProperty] private decimal formExchangeRate = 1;
    [ObservableProperty] private PaymentMethodOption? formMethod;
    [ObservableProperty] private decimal? formAmount;
    [ObservableProperty] private string? formDescription;
    [ObservableProperty] private DateTime? dueDate;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string addButtonText = "Kiritish";

    partial void OnFormCurrencyChanged(CurrencyViewModel? value)
    {
        if (value is not null)
            FormExchangeRate = value.IsDefault ? 1 : value.ExchangeRate > 0 ? value.ExchangeRate : 1;
    }

    private async Task InitAsync()
    {
        var curResp = await client.Currencies.GetAllAsync().Handle(l => IsLoading = l);
        if (curResp.IsSuccess && curResp.Data is not null)
            foreach (var c in mapper.Map<List<CurrencyViewModel>>(curResp.Data))
                Currencies.Add(c);

        FormCurrency = Currencies.FirstOrDefault(c => c.IsDefault) ?? Currencies.FirstOrDefault();
        FormMethod = PaymentMethods[0];

        var unResp = await client.Transactions.GetUnlinked(customerId, saleDate, saleId).Handle(l => IsLoading = l);
        if (unResp.IsSuccess && unResp.Data is not null)
            foreach (var p in unResp.Data)
                Payments.Add(new PaymentLineViewModel
                {
                    TransactionId = p.Id,
                    IsLinkedToSale = p.IsLinkedToSale,
                    // Biriktirilgan to'lovlar doim belgilangan; biriktirilmaganlari tahrirlashda
                    // belgilanmagan (adashib biriktirilganini olib tashlash uchun), yangi savdoda esa belgilangan.
                    IsSelected = p.IsLinkedToSale || !isSaleEdit,
                    Currency = Currencies.FirstOrDefault(c => c.Id == p.CurrencyId),
                    ExchangeRate = p.ExchangeRate,
                    PaymentMethod = p.PaymentMethod,
                    Amount = p.Amount,
                    Description = p.Description,
                    Date = p.Date,
                    Discount = p.Discount
                });
    }

    [RelayCommand]
    private void AddOrUpdate()
    {
        if (FormCurrency is null) { WarningMessage = "Valyutani tanlang."; return; }
        if (FormMethod is null) { WarningMessage = "To'lov usulini tanlang."; return; }
        if (FormAmount is null or <= 0) { WarningMessage = "Summa 0 dan katta bo'lishi kerak."; return; }

        if (IsEditing && editingLine is not null)
        {
            editingLine.Currency = FormCurrency;
            editingLine.ExchangeRate = FormExchangeRate;
            editingLine.PaymentMethod = FormMethod.Value;
            editingLine.Amount = FormAmount.Value;
            editingLine.Description = FormDescription;
            editingLine.IsSelected = true;
            // Mavjud to'lov tahrirlandi — saqlashda Update qilinadi.
            if (editingLine.IsExisting) editingLine.IsModified = true;
            editingLine = null;
            IsEditing = false;
            AddButtonText = "Kiritish";
        }
        else
        {
            Payments.Add(new PaymentLineViewModel
            {
                TransactionId = null,
                IsSelected = true,
                Currency = FormCurrency,
                ExchangeRate = FormExchangeRate,
                PaymentMethod = FormMethod.Value,
                Amount = FormAmount.Value,
                Description = FormDescription
            });
        }

        ClearForm();
    }

    [RelayCommand]
    private void Edit(PaymentLineViewModel? line)
    {
        if (line is null) return;
        editingLine = line;
        IsEditing = true;
        AddButtonText = "Yangilash";
        FormCurrency = line.Currency ?? FormCurrency;
        FormExchangeRate = line.ExchangeRate;
        FormMethod = PaymentMethods.FirstOrDefault(m => m.Value == line.PaymentMethod) ?? PaymentMethods[0];
        FormAmount = line.Amount;
        FormDescription = line.Description;
    }

    [RelayCommand]
    private async Task Delete(PaymentLineViewModel? line)
    {
        if (line is null) return;
        if (!Confirm("Ushbu to'lovni o'chirishni tasdiqlaysizmi?")) return;

        if (line.IsExisting)
        {
            var resp = await client.Transactions.Delete(line.TransactionId!.Value).Handle(l => IsLoading = l);
            if (!resp.IsSuccess) { ErrorMessage = resp.Message ?? "To'lovni o'chirishda xatolik."; return; }
        }

        Payments.Remove(line);
        if (ReferenceEquals(editingLine, line))
        {
            editingLine = null;
            IsEditing = false;
            AddButtonText = "Kiritish";
            ClearForm();
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (DueDate is { } due && due.Date <= saleDate.Date)
        {
            WarningMessage = "Qarzni to'lash sanasi to'lov sanasidan kamida bir kun keyin bo'lishi kerak.";
            return;
        }

        var attached = new List<AttachedPaymentLine>();

        // 1. Mavjud to'lovlarni solishtirish: belgilanganlar biriktiriladi, belgilanmaganlari
        // (ilgari biriktirilgan bo'lsa) uziladi. Yangi to'lovlardan oldin bajariladi.
        var existingIds = Payments.Where(p => p.IsExisting && p.IsSelected)
            .Select(p => p.TransactionId!.Value).ToList();

        var linkReq = new LinkPaymentsToSaleRequest
        {
            SaleId = saleId,
            TransactionIds = existingIds,
            DueDate = DueDate
        };

        var linkResp = await client.Transactions.LinkToSale(linkReq).Handle(l => IsLoading = l);
        if (!linkResp.IsSuccess) { ErrorMessage = linkResp.Message ?? "To'lovlarni biriktirishda xatolik."; return; }

        // 2. Oynada tahrirlangan mavjud to'lovlarni DB'da yangilaymiz (asl sana/chegirma saqlanadi).
        foreach (var line in Payments.Where(p => p.IsExisting && p.IsSelected && p.IsModified).ToList())
        {
            var updReq = new TransactionRequest
            {
                Id = line.TransactionId!.Value,
                Amount = line.Amount,
                ExchangeRate = line.ExchangeRate,
                Discount = line.Discount,
                PaymentMethod = line.PaymentMethod,
                IsIncome = true,
                Description = line.Description,
                Date = line.Date,
                DueDate = DueDate ?? line.Date,
                UserId = customerId,
                CurrencyId = line.Currency!.Id,
                SaleId = saleId
            };

            var updResp = await client.Transactions.Update(updReq).Handle(l => IsLoading = l);
            if (!updResp.IsSuccess) { ErrorMessage = updResp.Message ?? "To'lovni yangilashda xatolik."; return; }
            line.IsModified = false;
        }

        // 3. Yangi (belgilangan) to'lovlarni yaratamiz — ular yaratilishidayoq savdoga biriktiriladi.
        foreach (var line in Payments.Where(p => !p.IsExisting && p.IsSelected).ToList())
        {
            var req = new TransactionRequest
            {
                Amount = line.Amount,
                ExchangeRate = line.ExchangeRate,
                Discount = 0,
                PaymentMethod = line.PaymentMethod,
                IsIncome = true,
                Description = line.Description,
                Date = saleDate,
                DueDate = DueDate ?? saleDate,
                UserId = customerId,
                CurrencyId = line.Currency!.Id,
                SaleId = saleId
            };

            var resp = await client.Transactions.CreateAsync(req).Handle(l => IsLoading = l);
            if (!resp.IsSuccess) { ErrorMessage = resp.Message ?? "To'lov yaratishda xatolik."; return; }
            attached.Add(ToAttached(line));
        }

        attached.AddRange(Payments.Where(p => p.IsExisting && p.IsSelected).Select(ToAttached));

        Result = new PaymentLinkResult { Payments = attached, DueDate = DueDate };
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    private static AttachedPaymentLine ToAttached(PaymentLineViewModel p) => new()
    {
        Amount = p.Amount,
        CurrencyCode = p.CurrencyCode,
        ExchangeRate = p.ExchangeRate,
        MethodText = p.MethodText,
        Description = p.Description
    };

    private void ClearForm()
    {
        FormAmount = null;
        FormDescription = null;
        FormMethod = PaymentMethods[0];
        FormCurrency = Currencies.FirstOrDefault(c => c.IsDefault) ?? Currencies.FirstOrDefault();
    }
}
