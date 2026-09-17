using Xunit;

// Avalonia's headless platform keeps process-global rendering state and a single
// shared unit-test session. Running test classes in parallel let two tests drive
// that session at once, which made CaptureRenderedFrame intermittently return
// null (and, with async tests, threw PlatformNotSupportedException from
// Dispatcher.PushFrame). Serialising the assembly keeps the headless session
// single-tenant so screenshot capture is deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
