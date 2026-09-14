namespace OpdSimulator.App.Tests;

/// <summary>
/// Uses the case-insensitive prefix > substring ranking contract of
/// SearchFilter declared in docs/M5_UI_SPEC.md (FR-UI-6). A change in
/// ranking would fail these tests.
/// </summary>
public class SearchFilterTests
{
    [Theory]
    [InlineData("alice", "Alice")]
    [InlineData("AL", "alvin")]
    [InlineData("", "alice")]
    public void Filter_MatchesCaseInsensitively(string query, string expected)
    {
        var items = new[] { "alice", "alvin", "bob" };
        Assert.Contains(expected, SearchFilter.Filter(items, query), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filter_PrefixMatchesRankBeforeSubstringMatches()
    {
        var items = new[] { "receptionist", "screening", "recep" };

        var result = SearchFilter.Filter(items, "recep");

        Assert.Equal(new[] { "recep", "receptionist" }, result);
    }

    [Fact]
    public void Filter_EmptyQueryReturnsAllItemsInOriginalOrder()
    {
        var items = new[] { "zebra", "apple", "mango" };

        var result = SearchFilter.Filter(items, "   ");

        Assert.Equal(items, result);
    }

    [Fact]
    public void Filter_NoMatchesReturnsEmptyList()
    {
        Assert.Empty(SearchFilter.Filter(new[] { "alpha", "beta" }, "xyz"));
    }

    [Fact]
    public void Filter_NullItemsReturnsEmptyList()
    {
        Assert.Empty(SearchFilter.Filter(null, "anything"));
    }

    [Fact]
    public void Filter_NonMatchingItemsAreOmitted()
    {
        var result = SearchFilter.Filter(new[] { "alpha", "baker", "charlie" }, "ch");

        Assert.Contains("charlie", result);
        Assert.DoesNotContain("alpha", result);
        Assert.DoesNotContain("baker", result);
    }
}