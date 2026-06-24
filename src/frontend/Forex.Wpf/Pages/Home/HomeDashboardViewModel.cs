namespace Forex.Wpf.Pages.Home;

using CommunityToolkit.Mvvm.ComponentModel;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Responses;
using Forex.Wpf.Resources.Charts;
using System.Collections.ObjectModel;
using System.Windows.Media;

public partial class HomeDashboardViewModel : ObservableObject
{
    private readonly ForexClient client;

    [ObservableProperty] private string periodLabel = "so'nggi 7 kun";
    [ObservableProperty] private decimal rangeSales;
    [ObservableProperty] private int rangeCount;
    [ObservableProperty] private int rangePairs;
    [ObservableProperty] private decimal avgSale;

    [ObservableProperty] private ChartData salesTrend = new();
    [ObservableProperty] private ObservableCollection<ChartPoint> topCustomers = [];
    [ObservableProperty] private ObservableCollection<ChartPoint> topProducts = [];

    public HomeDashboardViewModel(ForexClient client) => this.client = client;

    public Task LoadAsync() => LoadAsync("Week");

    public async Task LoadAsync(string period)
    {
        try
        {
            var now = DateTime.Now;
            var today = DateTime.Today;

            DateTime begin;
            bool hourly = false;
            switch (period)
            {
                case "Day": begin = today; hourly = true; PeriodLabel = "bugun"; break;
                case "Month": begin = today.AddDays(-29); PeriodLabel = "so'nggi 30 kun"; break;
                default: begin = today.AddDays(-6); PeriodLabel = "so'nggi 7 kun"; break;
            }

            var request = new FilteringRequest
            {
                Filters = new()
                {
                    ["date"] = [$">={begin:o}", $"<{today.AddDays(1):o}"],
                    ["customer"] = ["include"],
                    ["saleItems"] = ["include:productType.product"]
                }
            };

            var response = await client.Sales.Filter(request).Handle();
            if (!response.IsSuccess || response.Data is null) return;

            var sales = response.Data;
            static double Base(SaleResponse s) => (double)(s.BaseAmount != 0 ? s.BaseAmount : s.TotalAmount);

            RangeSales = (decimal)sales.Sum(Base);
            RangeCount = sales.Count;
            RangePairs = sales.Where(s => s.SaleItems != null).SelectMany(s => s.SaleItems).Sum(i => i.TotalCount);
            AvgSale = RangeCount > 0 ? RangeSales / RangeCount : 0;

            // Mijozlar to'lovlari (kirim tranzaksiyalari) — ikkinchi chiziq uchun
            var txRequest = new FilteringRequest
            {
                Filters = new() { ["date"] = [$">={begin:o}", $"<{today.AddDays(1):o}"] }
            };
            var txResponse = await client.Transactions.Filter(txRequest).Handle();
            var payments = (txResponse.IsSuccess ? txResponse.Data : null) ?? [];
            static double Pay(TransactionResponse t) => t.IsIncome ? (double)(t.Amount * (t.ExchangeRate == 0 ? 1 : t.ExchangeRate)) : 0;

            var salesColor = Color.FromRgb(0x3B, 0x5B, 0xDB);
            var payColor = Color.FromRgb(0x1B, 0x7A, 0x3E);

            if (hourly)
            {
                var hours = Enumerable.Range(0, now.Hour + 1).ToList();
                var salesVals = hours.Select(h => sales
                    .Where(s => s.Date.ToLocalTime() >= today.AddHours(h) && s.Date.ToLocalTime() < today.AddHours(h + 1))
                    .Sum(Base)).ToList();
                var payVals = hours.Select(h => payments
                    .Where(t => t.Date.ToLocalTime() >= today.AddHours(h) && t.Date.ToLocalTime() < today.AddHours(h + 1))
                    .Sum(Pay)).ToList();
                SalesTrend = new ChartData
                {
                    Labels = [.. hours.Select(h => $"{h:00}:00")],
                    Series =
                    [
                        new ChartSeries { Name = "Savdo", Color = salesColor, Values = salesVals },
                        new ChartSeries { Name = "To'lovlar", Color = payColor, Values = payVals }
                    ]
                };
            }
            else
            {
                int days = period == "Month" ? 30 : 7;
                var dayList = Enumerable.Range(0, days).Select(i => begin.AddDays(i)).Where(d => d <= today).ToList();
                var salesVals = dayList.Select(d => sales.Where(s => s.Date.ToLocalTime().Date == d).Sum(Base)).ToList();
                var payVals = dayList.Select(d => payments.Where(t => t.Date.ToLocalTime().Date == d).Sum(Pay)).ToList();
                SalesTrend = new ChartData
                {
                    Labels = [.. dayList.Select(d => d.ToString("dd.MM"))],
                    Series =
                    [
                        new ChartSeries { Name = "Savdo", Color = salesColor, Values = salesVals },
                        new ChartSeries { Name = "To'lovlar", Color = payColor, Values = payVals }
                    ]
                };
            }

            TopCustomers = new ObservableCollection<ChartPoint>(sales
                .Where(s => s.Customer != null)
                .GroupBy(s => s.Customer.Name ?? "-")
                .Select(g => new ChartPoint { Label = g.Key, Value = g.Sum(Base) })
                .OrderByDescending(p => p.Value)
                .Take(5));

            TopProducts = new ObservableCollection<ChartPoint>(sales
                .Where(s => s.SaleItems != null)
                .SelectMany(s => s.SaleItems)
                .Where(i => i.ProductType?.Product != null)
                .GroupBy(i => i.ProductType.Product.Name ?? "-")
                .Select(g => new ChartPoint { Label = g.Key, Value = g.Sum(i => (double)i.TotalCount) })
                .OrderByDescending(p => p.Value)
                .Take(5));
        }
        catch
        {
        }
    }
}
