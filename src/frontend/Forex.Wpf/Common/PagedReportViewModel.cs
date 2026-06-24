namespace Forex.Wpf.Pages.Common;

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

public abstract partial class PagedReportViewModel<T> : ViewModelBase
{
    private List<T> source = [];

    [ObservableProperty] private ObservableCollection<T> pagedItems = [];
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int totalPages = 1;
    [ObservableProperty] private int totalCount;

    protected virtual int PageSize => 50;

    protected void SetSource(IEnumerable<T> items)
    {
        source = items as List<T> ?? [.. items];
        TotalCount = source.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(source.Count / (double)PageSize));
        if (CurrentPage != 1) CurrentPage = 1;
        else ApplyPage();
    }

    partial void OnCurrentPageChanged(int value) => ApplyPage();

    protected virtual void OnPageApplied() { }

    private void ApplyPage()
    {
        if (CurrentPage < 1) { CurrentPage = 1; return; }
        if (CurrentPage > TotalPages) { CurrentPage = TotalPages; return; }
        PagedItems = [.. source.Skip((CurrentPage - 1) * PageSize).Take(PageSize)];
        OnPageApplied();
    }
}
