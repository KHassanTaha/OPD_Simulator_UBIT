namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using System.Text;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Data.Preprocess;
using Serilog;

/// <summary>
/// <c>simulate-network</c>: run the multi-stage network directly from named
/// parameters (FR-SIM-1/2/3) instead of fitted data. The stage list, server
/// counts and service rates come from the command line; the internal arrival
/// rate is λ₀ = <c>--lambda</c>. This is the parameter-driven twin of the
/// fitted <c>simulate-data</c> path and carries the Milestone-3 day model
/// (D-009): <c>--days</c> runs over the clinic calendar (open Mon–Thu + Sat,
/// 08:15–11:00 arrival window, FR-SIM-6 drain), otherwise the classic
/// <c>--horizon</c> run is used.
/// </summary>
/// <remarks>
/// <para>
/// Routing: a probabilistic exit <c>--p-exit</c> is bound to the Screening
/// stage (the second-to-last stage), which requires at least three stages;
/// λ_doctor = λ₀·(1 − p_exit) then follows the routing law of D-007/D-015.
/// <c>--verbose</c> prints the pre-run routing-derived ρᵢ for every stage
/// before the run — the B3 trace aid showing exactly what determines
/// stability. A refusal lists every unstable stage with λᵢ, cᵢ, μᵢ, ρᵢ
/// (FR-VAL-1) and exits 1.
/// </para>
/// </remarks>
internal static class SimulateNetworkCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- simulate-network --lambda 0.2 --c 1,2,3 --mu 0.5,0.25,0.2 [--stages Reception,Screening,Doctor] [--p-exit 0.7] [--days N] [--start-day Monday] [--cap N] [--horizon <minutes>] [--seed <int>] [--verbose]\n" +
        "  --lambda        external arrival rate λ₀, patients/minute (required)\n" +
        "  --c             one server count per stage (required)\n" +
        "  --mu            one service rate μᵢ per stage, patients/minute/server (required)\n" +
        "  --stages        stage names, default the clinic flow (Reception, Screening, Doctor)\n" +
        "  --p-exit        probability of exiting after Screening (needs ≥ 3 stages; default 0)\n" +
        "  --days N        run N calendar day blocks (open Mon–Thu + Sat, window 08:15–11:00)\n" +
        "  --start-day     weekday of day 0, used with --days (default Monday)\n" +
        "  --cap N         daily admission cap, used with --days (default unlimited)\n" +
        "  --horizon       arrival-generation window in minutes, used without --days (default 10000)\n" +
        "  --seed          random seed, default 42 (FR-VAL-3)\n" +
        "  --verbose       print pre-run routing-derived ρᵢ per stage (B3)";

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

        int[] serverCounts = ParseIntList(GetOption(args, "--c"));
        if (serverCounts.Length == 0)
        {
            stderr.WriteLine("Missing required argument --c (one server count per stage, e.g. 1,2,3).");
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

        if (serverCounts.Length != mu.Length)
        {
            stderr.WriteLine($"--c has {serverCounts.Length} values but --mu has {mu.Length}; they must match, one per stage.");
            return 2;
        }

        double pExit = 0;
        string? pExitText = GetOption(args, "--p-exit");
        if (pExitText is not null && (!CliShared.TryParsePositive("--p-exit", pExitText, out pExit, out _) || pExit >= 1.0))
        {
            stderr.WriteLine($"Invalid --p-exit value '{pExitText}' (must be in [0, 1)).");
            return 2;
        }
        if (pExit > 0 && serverCounts.Length < 3)
        {
            stderr.WriteLine("--p-exit needs at least three stages (an exit stage plus a downstream stage); a last-stage 'exit' is not an exit.");
            return 2;
        }

        string[]? stageNames = ParseStageNames(GetOption(args, "--stages"));
        if (stageNames is not null && stageNames.Length != serverCounts.Length)
        {
            stderr.WriteLine($"--stages has {stageNames.Length} name(s) but --c/--mu have {serverCounts.Length}; they must match.");
            return 2;
        }
        stageNames ??= ClinicStageOrder.Flow.Take(serverCounts.Length).ToArray();

        int seed = SeededRandomSource.DefaultSeed;
        string? seedText = GetOption(args, "--seed");
        if (seedText is not null && !int.TryParse(seedText, out seed))
        {
            stderr.WriteLine($"Invalid --seed value '{seedText}'.");
            return 2;
        }

        string? daysText = GetOption(args, "--days");
        string? horizonText = GetOption(args, "--horizon");
        if (daysText is not null && horizonText is not null)
        {
            stderr.WriteLine("--days and --horizon are mutually exclusive run modes.");
            return 2;
        }

        DayOfWeek? startDay = null;
        string? startDayText = GetOption(args, "--start-day");
        if (startDayText is not null)
        {
            if (daysText is null)
            {
                stderr.WriteLine("--start-day requires --days (it names day 0 of the calendar).");
                return 2;
            }
            if (!TryParseWeekday(startDayText, out var day))
            {
                stderr.WriteLine($"Invalid --start-day '{startDayText}' (use a weekday name, e.g. Monday).");
                return 2;
            }
            startDay = day;
        }

        int? dailyCap = null;
        string? capText = GetOption(args, "--cap");
        if (capText is not null)
        {
            if (daysText is null)
            {
                stderr.WriteLine("--cap requires --days (the cap resets per calendar day block).");
                return 2;
            }
            if (!int.TryParse(capText, NumberStyles.None, CultureInfo.InvariantCulture, out int cap) || cap < 1)
            {
                stderr.WriteLine($"Invalid --cap value '{capText}' (must be a positive integer).");
                return 2;
            }
            dailyCap = cap;
        }

        int generatorDays = 0;
        if (daysText is not null && (!int.TryParse(daysText, NumberStyles.None, CultureInfo.InvariantCulture, out generatorDays) || generatorDays < 1))
        {
            stderr.WriteLine($"Invalid --days value '{daysText}' (must be a positive integer).");
            return 2;
        }

        double horizon = 10000;
        if (horizonText is not null && (!CliShared.TryParsePositive("--horizon", horizonText, out horizon, out _)))
        {
            stderr.WriteLine($"Invalid --horizon value '{horizonText}' (must be positive).");
            return 2;
        }

        var specs = Enumerable.Range(0, serverCounts.Length)
            .Select(i => new StageSpec(stageNames![i], serverCounts[i], mu[i]))
            .ToArray();
        int exitStageIndex = pExit > 0 ? serverCounts.Length - 2 : -1;

        var topology = new NetworkTopology(lambda, specs, exitStageIndex, pExit);

        bool verbose = args.Contains("--verbose");
        if (verbose)
            PrintPreRunRho(stdout, topology);

        var engine = new Engine(new SeededRandomSource(), Log.Logger);
        try
        {
            SimulationResult result = generatorDays > 0
                ? engine.Run(topology, new ClinicCalendar(), generatorDays, seed, dailyCap)
                : engine.Run(topology, seed, horizon);

            if (generatorDays > 0)
                PrintDayModel(stdout, generatorDays, startDay ?? DayOfWeek.Monday, dailyCap);

            Program.PrintNetworkMetrics(stdout, result);
            return 0;
        }
        catch (UnstableSystemException ex)
        {
            stderr.WriteLine($"Refusing to run: {ex.Message}");
            fileLogger.Error(ex, "Refusing to run an unstable simulate-network configuration");
            return 1;
        }
    }

    /// <summary>
    /// Prints the routing-derived per-stage ρᵢ = λᵢ/(cᵢ·μᵢ) for <c>--verbose</c>
    /// (B3): the λᵢ that flows into each stage after the probabilistic exit,
    /// together with the values that decide stability.
    /// </summary>
    private static void PrintPreRunRho(TextWriter stdout, NetworkTopology topology)
    {
        stdout.WriteLine("── Pre-run stability (routing-derived ρᵢ = λᵢ/(cᵢ·μᵢ)) ──");
        for (int i = 0; i < topology.StageSpecs.Count; i++)
        {
            var spec = topology.StageSpecs[i];
            stdout.WriteLine($"  Stage {i + 1} '{spec.Name}': λᵢ = {topology.EffectiveArrivalRate(i):0.###}/min, "
                             + $"c = {spec.ServerCount}, μ = {spec.ServiceRate:0.###}/min → ρ = {topology.RhoFor(i):0.##}");
        }
        stdout.WriteLine("─────────────────────────────────────────────────────");
        stdout.WriteLine();
    }

    private static void PrintDayModel(TextWriter stdout, int generatorDays, DayOfWeek startDay, int? dailyCap)
    {
        stdout.WriteLine("── Clinic day model ──");
        stdout.WriteLine($"  Generator days       : {generatorDays}");
        stdout.WriteLine($"  Start day            : {startDay}");
        stdout.WriteLine($"  Open weekdays        : Mon, Tue, Wed, Thu, Sat");
        stdout.WriteLine($"  Arrival window       : 08:15 – 11:00 (services drain past it)");
        stdout.WriteLine($"  Daily cap            : {(dailyCap.HasValue ? dailyCap.ToString() : "unlimited")}");
        stdout.WriteLine("─────────────────────────────────────────────────────");
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

    private static bool TryParseWeekday(string text, out DayOfWeek day)
        => Enum.TryParse(text, ignoreCase: true, out day);

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }
}