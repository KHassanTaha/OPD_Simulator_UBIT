namespace OpdSimulator.App.Tests;

/// <summary>
/// Guards the FR-UI-20 sort cycle: clicking a header advances
/// original → ascending → descending → original, and a new column sorts
/// ascending from original. The preview is a verification surface, so the
/// rows must survive sorting with their invalid badges intact.
/// </summary>
public class DataPreviewStoreTests
{
    private static DataPreviewStore BuildStore()
    {
        var store = new DataPreviewStore();
        store.SetData(
            new[] { "Token", "Arrival" },
            new[]
            {
                NewRow(new[] { "002", "09:05" }, 2),
                NewRow(new[] { "001", "09:00" }, 1),
                NewRow(new[] { "003", "09:02" }, 3, isInvalid: true, "negative value"),
            });
        return store;
    }

    private static DataPreviewRow NewRow(string[] cells, int sourceIndex,
        bool isInvalid = false, string? reason = null)
        => new()
        {
            Cells = cells,
            SourceIndex = sourceIndex,
            IsInvalid = isInvalid,
            ValidationReason = reason,
        };

    [Fact]
    public void SetData_ReplacesContentAndResetsSort()
    {
        var store = BuildStore();

        Assert.Equal(new[] { "Token", "Arrival" }, store.ColumnHeaders);
        Assert.Equal(3, store.View.Count);
        Assert.Null(store.ActiveSort);
    }

    [Fact]
    public void ToggleSort_AscendingFirst()
    {
        var store = BuildStore();

        store.ToggleSort(1);

        // Arrival ascending: 09:00, 09:02, 09:05
        Assert.Equal(new[] { "001", "003", "002" }, Tokens(store));
        Assert.Equal(new SortState(1, SortDirection.Ascending), store.ActiveSort);
    }

    [Fact]
    public void ToggleSort_CyclesAscendingDescendingOriginal()
    {
        var store = BuildStore();

        store.ToggleSort(0);
        Assert.Equal(SortDirection.Ascending, store.ActiveSort!.Direction);

        store.ToggleSort(0);
        Assert.Equal(SortDirection.Descending, store.ActiveSort!.Direction);

        store.ToggleSort(0);
        Assert.Null(store.ActiveSort);
        Assert.Equal(new[] { 1, 2, 3 }, store.View.Select(r => r.SourceIndex));
    }

    [Fact]
    public void ToggleSort_NewColumnStartsAscendingAndResetsOthers()
    {
        var store = BuildStore();

        store.ToggleSort(0);
        store.ToggleSort(1);

        Assert.Equal(new SortState(1, SortDirection.Ascending), store.ActiveSort);
    }

    [Fact]
    public void ToggleSort_OutOfRangeIsIgnored()
    {
        var store = BuildStore();

        store.ToggleSort(99);

        Assert.Null(store.ActiveSort);
        Assert.Equal(3, store.View.Count);
    }

    [Fact]
    public void Sorting_PreservesInvalidRowFlags()
    {
        var store = BuildStore();

        store.ToggleSort(1);

        var invalid = Assert.Single(store.View.Where(r => r.IsInvalid));
        Assert.Equal("negative value", invalid.ValidationReason);
        Assert.Equal("003", string.Join(",", invalid.Cells.Take(1)));
    }

    [Fact]
    public void ToggleSort_EqualValuesFallBackToSourceOrder()
    {
        var store = new DataPreviewStore();
        store.SetData(
            new[] { "Value" },
            new[]
            {
                NewRow(new[] { "same" }, 1),
                NewRow(new[] { "same" }, 2),
            });

        store.ToggleSort(0);

        Assert.Equal(new[] { 1, 2 }, store.View.Select(r => r.SourceIndex));
    }

    private static string[] Tokens(DataPreviewStore store)
        => store.View.Select(r => r.Cells[0]).ToArray();
}