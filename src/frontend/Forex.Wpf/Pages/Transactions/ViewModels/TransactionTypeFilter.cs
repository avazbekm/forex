namespace Forex.Wpf.Pages.Transactions.ViewModels;

using System.ComponentModel;

public enum TransactionTypeFilter
{
    [Description("Barchasi")]
    All,
    [Description("Kirim")]
    Income,
    [Description("Chiqim")]
    Expense
}