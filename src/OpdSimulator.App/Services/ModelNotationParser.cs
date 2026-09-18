using System;
using System.Collections.Generic;

namespace OpdSimulator.App.Services;

/// <summary>
/// Parses Kendall notation such as "M/M/1", "M/M/3", "M/D/2"
/// into the arrival distribution, service distribution, and
/// server count.
/// </summary>
public static class ModelNotationParser
{
    /// <summary>
    /// Result of parsing. Distribution families are returned as
    /// strings so the App layer does not depend on Core enums
    /// directly. The engine maps these strings to its own
    /// DistributionKind when building the topology.
    /// </summary>
    public sealed record ParsedModel(
        string ArrivalFamily,      // "Exponential" | "Deterministic" | "General"
        string ServiceFamily,
        int ServerCount);

    /// <summary>
    /// Standard list of model options shown in the UI dropdown.
    /// </summary>
    public static IReadOnlyList<string> StandardModels { get; } = new[]
    {
        "M/M/1", "M/M/2", "M/M/3", "M/M/4", "M/M/5",
        "M/D/1", "M/D/2", "M/D/3",
        "D/M/1", "D/M/2",
        "G/G/1",
    };

    /// <summary>
    /// Parse a model string. Throws ArgumentException with a
    /// clear message on malformed input.
    /// </summary>
    public static ParsedModel Parse(string notation)
    {
        var parts = notation.Split('/');
        if (parts.Length == 3
            && TryFamily(parts[0], out var arrival)
            && TryFamily(parts[1], out var service)
            && int.TryParse(parts[2], out var servers)
            && servers is >= 1 and <= 5)
        {
            return new ParsedModel(arrival, service, servers);
        }

        throw new ArgumentException(
            $"Invalid model notation '{notation}'. Expected A/S/c where A and S are M, D, or G, and c is an integer 1-5.");
    }

    private static bool TryFamily(string token, out string family)
    {
        family = token switch
        {
            "M" => "Exponential",
            "D" => "Deterministic",
            "G" => "General",
            _ => string.Empty,
        };
        return family.Length > 0;
    }
}
