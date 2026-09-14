namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Core.Trace;
using OpdSimulator.Data.Preprocess;
using Serilog;

/// <summary>
/// <c>trace</c>: rerun a network configuration and print a line-by-line,
/// human-readable event trace (Milestone 4). Each line is one state-changing
/// point — arrival, service start/end, route, exit — and, at
/// <c>--level rng</c>, one line per random draw with the sampled value.
/// </summary>
/// <remarks>
/// <para>
/// This is the viva proof artifact: the trace is deterministic (same seed → same
/// bytes), so its rows can be checked by hand against the numbers of the RNG and
/// the exponential transform. The run stops once <c>--patients</c> patients have
/// fully left the system, which keeps a trace short and reviewable.
/// </para>
/// <para>
/// The command deliberately runs the engine with a file-only logger (the CLI's
/// detail logger) so the trace is the only thing on the console — the engine's
/// Information-level "Simulation start" narration goes to the log files, not the
/// trace stream. With <c>--output</c> the trace goes to a file and a confirmation
/// line is printed; without it the trace is printed to stdout directly.
/// </para>
/// </remarks>
internal static class TraceCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- trace --lambda 0.5 --mu 2,1.5,1 --servers 1,2,3 [--p-exit 0.4] --patients 5 [--seed 42] [--level state] [--output trace.txt] [--real-start 08:15:00]\n" +
        "  --lambda     external arrival rate λ₀, patients/minute (required)\n" +
        "  --mu         one service rate μᵢ per stage, patients/minute/server (required)\n" +
        "  --servers    one server count cᵢ per stage (required)\n" +
        "  --stages     stage names, default the clinic flow (Reception, Screening, Doctor)\n" +
        "  --p-exit     probability of exiting after Screening (needs ≥ 3 stages; default 0)\n" +
        "  --patients   stop the run after this many patients have fully exited (required)\n" +
        "  --seed       random seed, default 42 (FR-VAL-3)\n" +
        "  --level      trace detail: events | state | rng (default state)\n" +
        "  --output     write the trace to this file instead of stdout\n" +
        "  --real-start wall-clock anchor of t = 0, HH:mm or HH:mm:ss (default 08:15:00)";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, ILogger fileLogger)
    {
        if (args.Contains("--help"))
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        if (!CliShared.TryParsePositive("--lambda", GetOption(args, "--lambda"), out double lambda, out string lambdaError))
        {
            stderr.WriteLine(lambdaError);
            stderr.WriteLine(Usage);
            return 2;
        }

        double[] mu = ParseDoubleList(GetOption(args, "--mu"));
        if (mu.Length == 0)
        {
            stderr.WriteLine("Missing required argument --mu (one service rate per stage, e.g. 0.5,0.25,0.2).");
            stderr.WriteLine(Usage);
            return 2;
        }

        int[] servers = ParseIntList(GetOption(args, "--servers"));
        if (servers.Length == 0)
        {
            stderr.WriteLine("Missing required argument --servers (one server count per stage, e.g. 1,2,3).");
            stderr.WriteLine(Usage);
            return 2;
        }

        if (servers.Length != mu.Length)
        {
            stderr.WriteLine($"--servers has {servers.Length} value(s) but --mu has {mu.Length}; they must match, one per stage.");
            return 2;
        }

        double pExit = 0;
        string? pExitText = GetOption(args, "--p-exit");
        if (pExitText is not null && (!CliShared.TryParsePositive("--p-exit", pExitText, out pExit, out _) || pExit >= 1.0))
        {
            stderr.WriteLine($"Invalid --p-exit value '{pExitText}' (must be in [0, 1)).");
            return 2;
        }
        if (pExit > 0 && servers.Length < 3)
        {
            stderr.WriteLine("--p-exit needs at least three stages (an exit stage plus a downstream stage).");
            return 2;
        }

        string? patientsText = GetOption(args, "--patients");
        if (patientsText is null || !int.TryParse(patientsText, NumberStyles.None, CultureInfo.InvariantCulture, out int patients) || patients < 1)
        {
            stderr.WriteLine(patientsText is null
                ? "Missing required argument --patients (the number of patients whose full journey to trace)."
                : $"Invalid --patients value '{patientsText}' (must be a positive integer).");
            stderr.WriteLine(Usage);
            return 2;
        }

        string[]? stageNames = ParseStageNames(GetOption(args, "--stages"));
        if (stageNames is not null && stageNames.Length != servers.Length)
        {
            stderr.WriteLine($"--stages has {stageNames.Length} name(s) but --servers/--mu have {servers.Length}; they must match.");
            return 2;
        }
        stageNames ??= ClinicStageOrder.Flow.Take(servers.Length).ToArray();

        int seed = SeededRandomSource.DefaultSeed;
        string? seedText = GetOption(args, "--seed");
        if (seedText is not null && !int.TryParse(seedText, out seed))
        {
            stderr.WriteLine($"Invalid --seed value '{seedText}'.");
            return 2;
        }

        string? levelText = GetOption(args, "--level");
        TraceLevel level = levelText switch
        {
            null or "state" => TraceLevel.State,
            "events" => TraceLevel.Events,
            "rng" => TraceLevel.Rng,
            _ => (TraceLevel)(-1),
        };
        if (level == (TraceLevel)(-1))
        {
            stderr.WriteLine($"Invalid --level '{levelText}' (use events, state or rng).");
            stderr.WriteLine(Usage);
            return 2;
        }

        double realStartMinutes = 495; // 08:15:00 — the clinic anchor (CONTEXT §5.1)
        string? realStartText = GetOption(args, "--real-start");
        if (realStartText is not null && !TryParseClock(realStartText, out realStartMinutes))
        {
            stderr.WriteLine($"Invalid --real-start '{realStartText}' (use HH:mm or HH:mm:ss).");
            return 2;
        }

        string? outputPath = GetOption(args, "--output");
        if (outputPath is { Length: 0 })
        {
            stderr.WriteLine("Invalid --output value (must be a non-empty path).");
            return 2;
        }

        var specs = Enumerable.Range(0, servers.Length)
            .Select(i => new StageSpec(stageNames![i], servers[i], mu[i]))
            .ToArray();
        int exitStageIndex = pExit > 0 ? servers.Length - 2 : -1;
        var topology = new NetworkTopology(lambda, specs, exitStageIndex, pExit);

        var engine = new Engine(new SeededRandomSource(), fileLogger);
        try
        {
            TextWriter? fileWriter = null;
            string outputFile = string.Empty;
            try
            {
                TextWriter writer = stdout;
                if (outputPath is not null)
                {
                    outputFile = System.IO.Path.GetFullPath(outputPath);
                    fileWriter = new StreamWriter(outputPath, append: false);
                    writer = fileWriter;
                }

                var sink = new TextWriterTraceSink(writer, level, realStartMinutes);
                SimulationResult result = engine.Run(topology, seed, horizonMinutes: 10_000, sink, patients);
                sink.Flush();
                writer.Flush();

                if (fileWriter is not null)
                    stdout.WriteLine($"Trace written to {outputFile} ({result.TotalPatientsServed} patient exit(s), level={level}, seed={seed}).");
            }
            finally
            {
                fileWriter?.Dispose();
            }

            return 0;
        }
        catch (UnstableSystemException ex)
        {
            stderr.WriteLine($"Refusing to run: {ex.Message}");
            fileLogger.Error(ex, "Refusing to run an unstable trace configuration");
            return 1;
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            stderr.WriteLine($"Cannot write trace output: {ex.Message}");
            return 2;
        }
    }

    private static bool TryParseClock(string text, out double minutes)
    {
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan ts))
        {
            minutes = ts.TotalMinutes;
            return minutes >= 0 && minutes < 24 * 60;
        }
        minutes = 0;
        return false;
    }

    private static int[] ParseIntList(string? text)
        => text is null
            ? Array.Empty<int>()
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : -1)
                .Where(v => v >= 1)
                .ToArray();

    private static double[] ParseDoubleList(string? text)
        => text is null
            ? Array.Empty<double>()
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => CliShared.TryParseDouble(t, out double v) ? v : -1.0)
                .Where(v => v > 0)
                .ToArray();

    private static string[]? ParseStageNames(string? text)
    {
        if (text is null)
            return null;
        string[] names = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return names.Length == 0 ? null : names;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }
}