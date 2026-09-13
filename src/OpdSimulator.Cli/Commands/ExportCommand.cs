namespace OpdSimulator.Cli.Commands;

using OpdSimulator.Data.Export;
using Serilog;

/// <summary>
/// <c>export</c>: write a validated file to a clean, analysis-ready CSV for SPSS
/// (FR-DATA-8). The output gets computed columns (arrival_minutes,
/// inter_arrival_minutes, per-stage start/end/service minutes) in addition to a
/// stable 1-based patient_id.
/// </summary>
internal static class ExportCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- export --file <path> [--output <path>]\n" +
        "  --file    .xlsx or .csv data file (required, must pass validation)\n" +
        "  --output  destination CSV path (default '<file stem>_export.csv' next to the input)";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, ILogger fileLogger)
    {
        if (args.Contains("--help"))
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        string? path = GetOption(args, "--file");
        if (path is null)
        {
            stderr.WriteLine("Missing required argument --file <path>.");
            stderr.WriteLine(Usage);
            return 2;
        }

        string output = GetOption(args, "--output")
            ?? Path.Combine(Path.GetDirectoryName(path) ?? ".", $"{Path.GetFileNameWithoutExtension(path)}_export.csv");

        if (!CliShared.TryLoadValidated(path, out var dataSet, out string errorLine))
        {
            stderr.WriteLine(errorLine);
            return 1;
        }

        try
        {
            DataExporter.ExportToCsv(dataSet!, output);
            stdout.WriteLine($"Wrote {dataSet!.RowCount} row(s) to {output}.");
            Log.Information("export: wrote {Rows} rows from {Path} to {Output}", dataSet.RowCount, path, output);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "export failed for {Path}", path);
            stderr.WriteLine($"Export failed: {ex.Message}");
            return 1;
        }
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }
}