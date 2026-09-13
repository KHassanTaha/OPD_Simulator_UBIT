namespace OpdSimulator.Data.Fitting;

/// <summary>
/// Constructs the distribution fitter for a named family (FR-STAT-1).
/// </summary>
/// <remarks>
/// The string keys are exactly the CLI/GUI values (case-insensitive) so the UI
/// dropdown and the <c>--distribution</c> argument map 1:1 onto this factory.
/// </remarks>
public static class DistributionFitterFactory
{
    private static readonly IDistributionFitter[] Fitters =
    {
        new ExponentialFitter(),
        new NormalFitter(),
        new LognormalFitter(),
        new GammaFitter(),
        new UniformFitter(),
    };

    /// <summary>All supported families, in display order.</summary>
    public static IReadOnlyList<string> SupportedNames => Fitters.Select(f => f.Name).ToList();

    /// <summary>
    /// Returns the fitter for a family name.
    /// </summary>
    /// <param name="name">Case-insensitive family name, e.g. <c>"exponential"</c>.</param>
    /// <param name="fitter">The matching fitter.</param>
    /// <returns><see langword="true"/> if the family is supported, else <see langword="false"/>.</returns>
    public static bool TryCreate(string name, out IDistributionFitter? fitter)
    {
        fitter = Fitters.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return fitter is not null;
    }

    /// <summary>
    /// Interns the list of supported names into one string for CLI error messages.
    /// </summary>
    /// <returns>e.g. "Exponential, Normal, Lognormal, Gamma, Uniform".</returns>
    public static string SupportedNamesText() => string.Join(", ", SupportedNames);
}