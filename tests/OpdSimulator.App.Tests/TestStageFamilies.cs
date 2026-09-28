using System.Collections.Generic;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Distributions;

namespace OpdSimulator.App.Tests;

/// <summary>
/// The per-stage family list the pre-8L tests used to pass as one string.
/// </summary>
/// <remarks>
/// Phase 8L replaced <c>InputAnalysisService.FitAll</c>'s single service-family
/// string with one entry per stage. Tests that were written when one global
/// "Exponential" drove every stage still mean exactly that, so they call this
/// instead of spelling out a list at each of the ten call sites. The stage names
/// come from the binding under test, so a data file with three stages gets three
/// entries and a two-stage file gets two — the mapping is by name, not by index.
/// </remarks>
internal static class TestStageFamilies
{
    /// <summary>
    /// Every stage of <paramref name="binding"/> configured Exponential, which is
    /// what the old single-string argument did. A null binding yields an empty
    /// list: <c>FitAll</c> returns before reading any family, and an empty list
    /// says "nothing configured" rather than inventing a family.
    /// </summary>
    internal static IReadOnlyList<StageServiceFamily> AllExponential(DataBindingResult? binding)
        => binding is null
            ? new List<StageServiceFamily>()
            : binding.StageNames
                .Select(name => new StageServiceFamily(name, DistributionFamily.Exponential))
                .ToList();
}
