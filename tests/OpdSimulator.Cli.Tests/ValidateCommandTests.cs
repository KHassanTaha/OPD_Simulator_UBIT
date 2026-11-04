namespace OpdSimulator.Cli.Tests;

using Serilog;

/// <summary>
/// Verifies the <c>validate</c> command writes the report it promises, at the path
/// it was given, with the model the caller selected (FR-VAL-6, D-210).
/// </summary>
/// <remarks>
/// The assertion reads the file from disk rather than trusting the exit code or the
/// stdout line (D-186): a command that returned 0 and wrote nothing would satisfy a
/// return-code check, and one that wrote to the wrong path would satisfy a "value in
/// memory" check.
/// </remarks>
public class ValidateCommandTests
{
    [Fact]
    public void Validate_WritesReportAtTheGivenPath_WithTheSelectedModel()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opd-validation-{Guid.NewGuid():N}.txt");

        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(
                new[]
                {
                    "validate", "--lambda", "0.5", "--mu", "0.8", "--seed", "42",
                    "--model", "M/G/c", "--cs2", "2", "--out", path
                },
                stdout,
                stderr,
                Log.Logger);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(path), $"expected the report at {path}");

            string report = File.ReadAllText(path);
            Assert.Contains("VALIDATION REPORT", report);
            Assert.Contains("M/G/c", report);
            Assert.Contains("Pollaczek", report);
            Assert.Contains("0.5", report);
            Assert.Contains("sensitivity analysis", report);
            Assert.Contains("N/A (no verdict issued)", report);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Validate_RefusesAnUnknownModel_WithoutWritingAFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opd-validation-{Guid.NewGuid():N}.txt");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(
            new[] { "validate", "--lambda", "0.5", "--mu", "0.8", "--model", "M/X/c", "--out", path },
            stdout,
            stderr,
            Log.Logger);

        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown model", stderr.ToString());
        Assert.False(File.Exists(path), "a refused invocation must not leave a report behind");
    }
}
