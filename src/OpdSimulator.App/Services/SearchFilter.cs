using System;
using System.Collections.Generic;
using System.Linq;

namespace OpdSimulator.App.Services;

/// <summary>
/// Pure type-to-filter logic for searchable dropdowns (FR-UI-6). Kept free
/// of UI types so it is unit-testable: filters case-insensitively and ranks
/// matches — exact prefix first, then substring — otherwise in item order.
/// </summary>
public static class SearchFilter
{
    /// <summary>
    /// Returns the items matching <paramref name="filter"/>, ranked prefix-first.
    /// An empty or whitespace filter returns every item unchanged.
    /// </summary>
    public static IReadOnlyList<string> Filter(IEnumerable<string>? items, string? filter)
    {
        if (items is null)
        {
            return Array.Empty<string>();
        }

        var query = filter?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return items.ToArray();
        }

        return items
            .Select(item => new { Item = item, Rank = Rank(item, query) })
            .Where(match => match.Rank >= 0)
            .OrderBy(match => match.Rank)
            .ThenBy(match => match.Item, StringComparer.Ordinal)
            .Select(match => match.Item)
            .ToArray();
    }

    private static int Rank(string item, string query)
    {
        var index = item.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        return index switch
        {
            < 0 => -1,   // no match
            0 => 0,      // prefix match — best
            _ => 1,      // substring match — second best
        };
    }
}