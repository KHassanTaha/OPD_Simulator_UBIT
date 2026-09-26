namespace OpdSimulator.Core.Distributions;

/// <summary>Supported distribution families for sampling.
/// <para>Enum order is part of the public contract (e.g. persisted presets): existing
/// values must keep their numbers. Append new members at the end only.</para></summary>
public enum DistributionFamily
{
    /// <summary>Exponential, parameterised by mean.</summary>
    Exponential = 0,

    /// <summary>Deterministic, always returns the mean.</summary>
    Deterministic = 1,

    /// <summary>Normal (Gaussian), parameterised by mean and standard deviation.</summary>
    Normal = 2,

    /// <summary>Lognormal, parameterised by mean and standard deviation of X.</summary>
    Lognormal = 3,

    /// <summary>Gamma, parameterised by shape and scale.</summary>
    Gamma = 4,

    /// <summary>Continuous uniform, parameterised by min and max.</summary>
    Uniform = 5
}
