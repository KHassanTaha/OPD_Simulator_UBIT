namespace OpdSimulator.Cli.Commands;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using OpdSimulator.Data.Validation;
using Serilog;

/// <summary>
/// <c>verify</c>: load a data file and validate it (FR-DATA-7). Prints every
/// issue found — never a summary hiding individual rows — and exits 0 when the
/// file is clean, 1 when any issue exists.
/// </summary>
internal static class VerifyCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- verify --file <path>\n" +
        "  --file   .xlsx or .csv data file (required)\n" +
        "Exit codes: 0 = valid; 1 = validation issues were found (all printed to stdout).";

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

        try
        {
            var loader = new DataLoaderFactory().Create(path);
            DataSet data = loader.Load(path);

            IReadOnlyList<ValidationIssue> issues = DataValidator.ValidateReturningIssues(data);
            var stages = StagePairDetector.Detect(data.Columns);

            if (issues.Count == 0)
            {
                stdout.WriteLine($"File is valid: {data.RowCount} row(s), {stages.Pairs.Count} service stage pair(s).");
                Log.Information("verify: {Path} is valid ({Rows} rows)", path, data.RowCount);
                return 0;
            }

            foreach (var issue in issues)
                stdout.WriteLine(issue);
            stdout.WriteLine($"Validation failed: {issues.Count} issue(s) in '{path}'.");
            Log.Warning("verify: {Path} failed validation with {Count} issue(s)", path, issues.Count);
            return 1;
        }
        catch (DataValidationException)
        {
            // Defensive: ValidateReturningIssues never throws for issues; keep the
            // standalone validator contract covered here regardless.
            stderr.WriteLine($"Validation of '{path}' failed.");
            return 1;
        }
        catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidDataException)
        {
            stderr.WriteLine(ex.Message);
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