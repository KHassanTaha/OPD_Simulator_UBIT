using Xunit;

// The CLI tests share the static Serilog Log.Logger instance (Program.Run uses
// it), so xUnit's default per-class parallelisation corrupts CliRefusalTests'
// sink-event assertion with concurrent log writes. Serialise the assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Optional collection definition ensuring the CLI test classes (which share
/// the static Serilog global) never run concurrently. See the assembly-level
/// <see cref="CollectionBehaviorAttribute"/> above.
/// </summary>
[CollectionDefinition("Cli")]
public class CliTestCollection
{
}