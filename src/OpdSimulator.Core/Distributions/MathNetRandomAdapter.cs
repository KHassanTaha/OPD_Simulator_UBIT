namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Presents a project <see cref="IRandomSource"/> to MathNet.Numerics, whose
/// distribution constructors require a <see cref="Random"/>.
/// </summary>
/// <remarks>
/// <para>
/// MathNet 5.0.0 takes <see cref="Random"/> in every distribution constructor
/// (<c>Normal(mean, sd, random)</c> and friends). The 4.x overloads took a
/// <c>MathNet.Numerics.Random.IRandomSource</c>, but that type no longer exists in
/// the 5.0.0 assembly, so the usual thin wrapper cannot be built against it.
/// </para>
/// <para>
/// The bridge is possible because every <see cref="Random"/> instance method is
/// <c>virtual</c> on .NET 8. Each override below forwards to the wrapped
/// <see cref="IRandomSource"/>, so MathNet consumes the project's deviates and the
/// whole run stays reproducible from one seed (FR-VAL-3, NFR-4). All overridable
/// members are overridden deliberately: a forgotten method would fall through to
/// the base class's own generator and quietly desynchronise the sequence.
/// <c>MathNetRandomAdapter_DelegatesEveryOverriddenMethodToSource</c> locks this
/// down.
/// </para>
/// <para>
/// A constant base seed is supplied to <see cref="Random"/> as a determinism
/// backstop. <c>NextDouble(double)</c> and <c>NextBoolean()</c> exist on
/// <see cref="Random"/> but are not <c>virtual</c>, so a call to one of those would
/// read base state rather than the project source. The families used here only call
/// <see cref="NextDouble"/>, so base state is never consulted in practice.
/// </para>
/// </remarks>
public sealed class MathNetRandomAdapter : Random
{
    /// <summary>
    /// Fixed base seed. Never used by the overridden members; see the remarks.
    /// </summary>
    private const int BaseSeed = 0;

    private readonly IRandomSource _source;

    /// <summary>
    /// Wraps <paramref name="source"/> so it can be passed to MathNet.
    /// </summary>
    /// <param name="source">The project's uniform deviate source.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is null.</exception>
    public MathNetRandomAdapter(IRandomSource source)
        : base(BaseSeed)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>Draws one deviate from the wrapped source.</summary>
    public override double NextDouble() => _source.NextDouble();

    /// <summary>Draws one deviate from the wrapped source and scales it to [0, int.MaxValue).</summary>
    public override int Next() => (int)(_source.NextDouble() * int.MaxValue);

    /// <inheritdoc cref="Next(int)"/>
    public override int Next(int maxValue)
    {
        if (maxValue < 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "maxValue must be non-negative.");

        return (int)(_source.NextDouble() * maxValue);
    }

    /// <inheritdoc cref="Next(int,int)"/>
    public override int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "minValue must not exceed maxValue.");

        if (minValue == maxValue)
            return minValue;

        // long arithmetic so a span wider than int.MaxValue cannot overflow.
        return (int)(minValue + (_source.NextDouble() * (maxValue - (long)minValue)));
    }

    /// <inheritdoc cref="NextInt64()"/>
    public override long NextInt64() => (long)(_source.NextDouble() * long.MaxValue);

    /// <inheritdoc cref="NextInt64(long)"/>
    public override long NextInt64(long maxValue)
    {
        if (maxValue < 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "maxValue must be non-negative.");

        return (long)(_source.NextDouble() * maxValue);
    }

    /// <inheritdoc cref="NextInt64(long,long)"/>
    public override long NextInt64(long minValue, long maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "minValue must not exceed maxValue.");

        if (minValue == maxValue)
            return minValue;

        // double arithmetic on the span, then an additive long, so the result
        // stays inside [minValue, maxValue) without 64-bit overflow.
        return (long)(minValue + (_source.NextDouble() * (maxValue - (double)minValue)));
    }

    /// <summary>Draws one deviate and returns it as a single-precision value.</summary>
    public override float NextSingle() => (float)_source.NextDouble();

    /// <inheritdoc cref="NextBytes(byte[])"/>
    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        NextBytes(buffer.AsSpan());
    }

    /// <inheritdoc cref="NextBytes(Span{byte})"/>
    public override void NextBytes(Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = NextByte();
    }

    /// <summary>
    /// Maps one deviate onto a full-range byte. Multiplying by 256 (not 255) keeps
    /// the mapping uniform; <see cref="IRandomSource.NextDouble"/> is [0, 1), so the
    /// result can never be 256.
    /// </summary>
    private byte NextByte() => (byte)(_source.NextDouble() * 256.0);
}
