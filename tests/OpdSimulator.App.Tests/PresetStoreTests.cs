using OpdSimulator.App.Services;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Minimum preset-system tests mandated by AGENTS §17.3: round-trip,
/// schema mismatch, missing data file, sanitisation, collisions, path
/// resolution under ApplicationData and import/export. All run headless —
/// the store is pure <c>System.Text.Json</c> + file I/O in a temp directory.
/// </summary>
public sealed class PresetStoreTests
{
    private static Preset Sample(string name = "Demo-3stage") => new()
    {
        Name = name,
        Config = new PresetConfig
        {
            ParameterMode = "rate",
            InterArrivalDistribution = "exponential",
            ServiceDistribution = "exponential",
            ArrivalParameter = "0.3",
            Servers = new PresetStageNumbers { Reception = 1, Screening = 2, Doctor = 3 },
            ServiceRates = new PresetStageRates { Reception = "0.5", Screening = "0.5", Doctor = "0.5" },
            Horizon = new PresetHorizon { Mode = "days", Value = "3" },
            StartDay = "Monday",
            DailyCap = null,
            RandomSeed = "42",
            PExitOverride = "0.4",
            TraceLevel = "state",
        },
        DataFile = "samples/sample_patients.xlsx",
        View = new PresetView { VisibleWidgets = new[] { "metrics", "chiSquare", "trace" } },
    };

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "OpdSimulatorPresetTests", Guid.NewGuid().ToString("N"));
        return dir;
    }

    [Fact]
    public void SaveAndLoad_RoundTripsEveryField()
    {
        string dir = TempDir();
        try
        {
            var store = new PresetStore(dir);
            var original = Sample();
            store.Save(original);

            var loaded = store.Load("Demo-3stage");

            Assert.Equal("Demo-3stage", loaded.Name);
            Assert.Equal(Preset.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal("0.3", loaded.Config!.ArrivalParameter);
            Assert.Equal(1, loaded.Config.Servers!.Reception);
            Assert.Equal(2, loaded.Config.Servers.Screening);
            Assert.Equal(3, loaded.Config.Servers.Doctor);
            Assert.Equal("Monday", loaded.Config.StartDay);
            Assert.Equal("0.4", loaded.Config.PExitOverride);
            Assert.Equal("samples/sample_patients.xlsx", loaded.DataFile);
            Assert.Equal(new[] { "metrics", "chiSquare", "trace" }, loaded.View!.VisibleWidgets);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_SchemaMismatch_ThrowsClearError()
    {
        string dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "OldSchema.json"),
                """{"schemaVersion": 99, "name": "OldSchema", "config": {"parameterMode": "rate"}}""");

            var store = new PresetStore(dir);

            var ex = Assert.Throws<PresetException>(() => store.Load("OldSchema"));

            Assert.Contains("99", ex.Message);
            Assert.Contains(Preset.CurrentSchemaVersion.ToString(), ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_SchemaMismatch_ThrowsClearError()
    {
        var store = new PresetStore(TempDir());

        var ex = Assert.Throws<PresetException>(() => store.Save(new Preset { Name = "Broken", SchemaVersion = 2 }));

        Assert.Contains("schema 2", ex.Message);
        Assert.Contains("schema 1", ex.Message);
    }

    [Fact]
    public void MissingDataFile_DoesNotStopPresetFromLoading()
    {
        string dir = TempDir();
        try
        {
            var store = new PresetStore(dir);
            var preset = Sample("MissingFile");
            preset.DataFile = "C:/does/not/exist.xlsx";
            store.Save(preset);

            var loaded = store.Load("MissingFile");

            Assert.Equal("C:/does/not/exist.xlsx", loaded.DataFile);
            Assert.Equal("0.3", loaded.Config!.ArrivalParameter);
            Assert.NotNull(loaded.Config.Servers);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Rename_Collision_ThrowsDeterministicError()
    {
        string dir = TempDir();
        try
        {
            var store = new PresetStore(dir);
            store.Save(Sample("Target"));
            store.Save(Sample("Clash"));

            var ex = Assert.Throws<PresetException>(() => store.Rename("Target", "Clash"));

            Assert.Contains("already exists", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Duplicate_Collision_ThrowsDeterministicError()
    {
        string dir = TempDir();
        try
        {
            var store = new PresetStore(dir);
            store.Save(Sample("Same"));
            store.Save(Sample("Other"));

            var ex = Assert.Throws<PresetException>(() => store.Duplicate("Same", "Other"));

            Assert.Contains("already exists", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PathResolution_IsUnderApplicationData_NotCurrentDirectory()
    {
        string root = PresetStore.DefaultDirectory;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Equal(Path.Combine(appData, "OpdSimulator", "presets"), root);
        Assert.StartsWith(appData, root, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ThenImportFromFreshStore_RoundTripsCleanly()
    {
        string dir1 = TempDir();
        string dir2 = TempDir();
        try
        {
            var first = new PresetStore(dir1);
            first.Save(Sample("Shipped"));
            string exportPath = Path.Combine(Path.GetTempPath(), "OpdSimulatorPresetTests", Guid.NewGuid().ToString("N"), "shipped.json");

            first.Export("Shipped", exportPath);

            var second = new PresetStore(dir2);
            string importedName = second.Import(exportPath);

            var reimported = second.Load(importedName);
            Assert.Equal("Shipped", reimported.Name);
            Assert.Equal("0.3", reimported.Config!.ArrivalParameter);
            Assert.Equal(2, reimported.Config.Servers!.Screening);
        }
        finally
        {
            Directory.Delete(dir1, recursive: true);
            if (Directory.Exists(dir2)) Directory.Delete(dir2, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_CaseInsensitiveNames_BehaveIdentically()
    {
        string dir = TempDir();
        try
        {
            var store = new PresetStore(dir);
            store.Save(Sample("Casey"));

            Assert.True(store.Exists("CASEY"));
            var loaded = store.Load("casey");

            Assert.Equal("Casey", loaded.Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void List_IsSortedCaseInsensitively()
    {
        string dir = TempDir();
        try
        {
            var store = new PresetStore(dir);
            store.Save(Sample("beta"));
            store.Save(Sample("Alpha"));
            store.Save(Sample("gamma"));

            var names = store.List();

            Assert.Equal(new[] { "Alpha", "beta", "gamma" }, names);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Sanitize_StripsReservedAndControlCharacters()
    {
        Assert.Equal("Demo-3stage-UnstableDoctor", PresetNaming.Sanitize("Demo/\\:*?\"<>|-3stage\t-UnstableDoctor"));
        Assert.Equal("plain", PresetNaming.Sanitize("  plain  "));
        Assert.Equal(string.Empty, PresetNaming.Sanitize(null));
        Assert.Equal(string.Empty, PresetNaming.Sanitize(" /\\:*?\"<>| "));
    }

    [Fact]
    public void Save_EmptyNameAfterSanitise_Throws()
    {
        var store = new PresetStore(TempDir());

        var ex = Assert.Throws<PresetException>(() => store.Save(Sample(" /\\:*?\"<>| ")));

        Assert.Contains("empty", ex.Message);
    }
}