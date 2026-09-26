using System;
using System.Collections.Generic;
using OpdSimulator.Core.Distributions;

namespace OpdSimulator.App.Services;

/// <summary>
/// Parses Kendall notation such as "M/M/1", "M/M/3", "M/D/2"
/// into the arrival distribution, service distribution, and
/// server count.
/// </summary>
public static class ModelNotationParser
{
    /// <summary>
    /// Result of parsing. Both families are nullable because "G" carries no
    /// family: G is short for "General", meaning <em>whatever the data says</em>,
    /// so a G/G/c notation deliberately resolves to <see langword="null"/> on both
    /// sides and the caller resolves it — by auto-fit, or by the user picking a
    /// concrete family in the row's Advanced mode (Phase 8K, D-150).
    /// </summary>
    /// <remarks>
    /// These are the Core <see cref="DistributionFamily"/> members rather than the
    /// strings this record used to carry. The old shape had to invent a third
    /// pseudo-family, "General", that existed nowhere in the engine — a value the
    /// topology builder could not use and every consumer had to special-case.
    /// Null says the same thing honestly: no family has been chosen.
    /// </remarks>
    /// <param name="ArrivalFamily">Arrival family, or null for a G (general/auto-fit) notation.</param>
    /// <param name="ServiceFamily">Service family, or null for a G (general/auto-fit) notation.</param>
    /// <param name="ServerCount">Parallel server count c.</param>
    public sealed record ParsedModel(
        DistributionFamily? ArrivalFamily,
        DistributionFamily? ServiceFamily,
        int ServerCount);

    /// <summary>
    /// Standard list of model options shown in the UI dropdown.
    /// </summary>
    /// <remarks>
    /// Fifteen entries, in the order the owner fixed (Phase 8K, Ruling 4): the five
    /// M/M/c counts, the three M/D/c counts, the two D/M/c counts, then the five
    /// G/G/c counts. G/G/c is offered for every count 1–5, not just 1, so a
    /// three-stage network can auto-fit a family per stage.
    /// </remarks>
    public static IReadOnlyList<string> StandardModels { get; } = new[]
    {
        "M/M/1", "M/M/2", "M/M/3", "M/M/4", "M/M/5",
        "M/D/1", "M/D/2", "M/D/3",
        "D/M/1", "D/M/2",
        "G/G/1", "G/G/2", "G/G/3", "G/G/4", "G/G/5",
    };

    /// <summary>
    /// The notation prefix that means "auto-fit the family from data" (Phase 8K).
    /// </summary>
    public const string GeneralToken = "G/G/";

    /// <summary>
    /// True when a notation is the G/G/c auto-fit family, i.e. its first two
    /// tokens are both "G".
    /// </summary>
    /// <param name="notation">A Kendall notation such as "G/G/3".</param>
    /// <returns>True for any G/G/c notation, false otherwise.</returns>
    public static bool IsGeneralModel(string? notation) =>
        notation is not null && notation.StartsWith(GeneralToken, StringComparison.Ordinal);

    /// <summary>
    /// Parse a model string. Throws ArgumentException with a
    /// clear message on malformed input.
    /// </summary>
    /// <param name="notation">A Kendall notation such as "M/M/2" or "G/G/1".</param>
    /// <returns>The families and server count; both families null for G/G/c.</returns>
    /// <exception cref="ArgumentException">If the notation is malformed.</exception>
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

    private static bool TryFamily(string token, out DistributionFamily? family)
    {
        // "G" parses successfully but yields null: it is not a family the engine can
        // sample, it is an instruction to choose one, so the caller decides how
        // (auto-fit from data, or an explicit pick in the row's Advanced mode).
        switch (token)
        {
            case "M":
                family = DistributionFamily.Exponential;
                return true;
            case "D":
                family = DistributionFamily.Deterministic;
                return true;
            case "G":
                family = null;
                return true;
            default:
                family = null;
                return false;
        }
    }
}
