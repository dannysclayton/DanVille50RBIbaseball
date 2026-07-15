using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class LeagueStoreRecoveryTests
{
    [Fact]
    public void NewLeague_UsesCurrentSaveSchemaVersion()
    {
        Assert.Equal(LeagueStore.CurrentSaveSchemaVersion, new LeagueFile().SaveSchemaVersion);
    }

    [Fact]
    public void NewLeague_DoesNotDefaultToADeveloperMachineAssetLibrary()
    {
        var league = new LeagueFile();

        Assert.Empty(LeagueFile.DefaultAssetLibraryPath);
        Assert.Empty(league.AssetLibraryPath);
    }

    [Fact]
    public void Load_SaveWithoutAssetLibraryPropertyLeavesLibraryUnconfigured()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "legacy" + LeagueStore.Extension);
        try
        {
            File.WriteAllText(path, "{\"Name\":\"Portable Dynasty\"}");

            LeagueFile league = LeagueStore.Load(path);

            Assert.Equal("Portable Dynasty", league.Name);
            Assert.Equal(string.Empty, league.AssetLibraryPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SaveAndLoad_PreservesAnExplicitAssetLibraryPath()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "configured" + LeagueStore.Extension);
        string libraryPath = Path.Combine(directory, "Media Library");
        try
        {
            var league = new LeagueFile { AssetLibraryPath = libraryPath };

            LeagueStore.Save(path, league);
            LeagueFile loaded = LeagueStore.Load(path);

            Assert.Equal(libraryPath, loaded.AssetLibraryPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void AssetLibraryManager_CreateBuildsPortableLibraryFoldersWithoutDeletingContent()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "User Library");
        try
        {
            Directory.CreateDirectory(path);
            string existingFile = Path.Combine(path, "keep.txt");
            File.WriteAllText(existingFile, "keep");

            string createdPath = AssetLibraryManager.Create(path);

            Assert.Equal(Path.GetFullPath(path), createdPath);
            Assert.True(File.Exists(existingFile));
            Assert.All(AssetLibraryManager.StandardFolders,
                folder => Assert.True(Directory.Exists(Path.Combine(createdPath, folder)), folder));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void TryLoad_ReturnsAnErrorInsteadOfThrowingForDamagedDynasty()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "damaged" + LeagueStore.Extension);
        try
        {
            File.WriteAllText(path, "{ this is not valid dynasty json");

            bool loaded = LeagueStore.TryLoad(path, out var league, out string error);

            Assert.False(loaded);
            Assert.Null(league);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Load_MigratesVersion1CoachHallEntryToCurrentSchema()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "version-one" + LeagueStore.Extension);
        Guid coachId = Guid.NewGuid();
        try
        {
            File.WriteAllText(path, $$"""
                {
                  "SaveSchemaVersion": 1,
                  "Name": "Legacy Dynasty",
                  "HallOfFameEntries": [
                    {
                      "EntryType": "Coach",
                      "PlayerId": "{{coachId}}",
                      "PlayerName": "Legacy Coach"
                    }
                  ]
                }
                """);

            LeagueFile league = LeagueStore.Load(path);

            Assert.Equal(LeagueStore.CurrentSaveSchemaVersion, league.SaveSchemaVersion);
            HallOfFameEntry entry = Assert.Single(league.HallOfFameEntries);
            Assert.Equal(coachId, entry.CoachId);
            Assert.Equal("Legacy Coach", entry.CoachName);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Load_TreatsMissingSchemaVersionAsVersion1AndRunsMigrations()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "unversioned" + LeagueStore.Extension);
        try
        {
            File.WriteAllText(path, "{\"Name\":\"Unversioned Dynasty\",\"InboxMessages\":null}");

            LeagueFile league = LeagueStore.Load(path);

            Assert.Equal(LeagueStore.CurrentSaveSchemaVersion, league.SaveSchemaVersion);
            Assert.NotNull(league.InboxMessages);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void TryLoad_RejectsSchemaFromNewerApplicationVersion()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "future" + LeagueStore.Extension);
        try
        {
            File.WriteAllText(path, "{\"SaveSchemaVersion\":" + (LeagueStore.CurrentSaveSchemaVersion + 1) + "}");

            bool loaded = LeagueStore.TryLoad(path, out LeagueFile league, out string error);

            Assert.False(loaded);
            Assert.Null(league);
            Assert.Contains("supports up to version", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void GetBackups_FindsValidRecoveryCopiesWhenPrimaryIsDamaged()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "season-one" + LeagueStore.Extension);
        try
        {
            var league = new LeagueFile { Name = "Recoverable Dynasty" };
            LeagueStore.Save(path, league);
            league.Name = "Recoverable Dynasty Updated";
            LeagueStore.Save(path, league);

            string backupDirectory = Path.Combine(directory, "Backups");
            string invalidBackup = Path.Combine(
                backupDirectory,
                Path.GetFileNameWithoutExtension(path) + ".invalid" + Path.GetExtension(path) + ".bak");
            File.WriteAllText(invalidBackup, "invalid backup");
            File.WriteAllText(path, "damaged primary");

            Assert.False(LeagueStore.TryLoad(path, out _, out _));
            var backups = LeagueStore.GetBackups(path);

            Assert.Contains(backups, backup => backup.IsValid);
            Assert.Contains(backups, backup => !backup.IsValid && backup.Path == invalidBackup);
            var validBackup = backups.First(backup => backup.IsValid);
            Assert.True(LeagueStore.TryLoad(validBackup.Path, out var recovered, out string error), error);
            Assert.Equal("Recoverable Dynasty", recovered.Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void TryLoadWithRecovery_UsesNewestValidBackupWhenPrimaryIsDamaged()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "recover-latest" + LeagueStore.Extension);
        try
        {
            var league = new LeagueFile { Name = "Version 1" };
            LeagueStore.Save(path, league);
            league.Name = "Version 2";
            LeagueStore.Save(path, league);
            league.Name = "Version 3";
            LeagueStore.Save(path, league);
            File.WriteAllText(path, "damaged primary");

            bool loaded = LeagueStore.TryLoadWithRecovery(
                path,
                out LeagueFile recovered,
                out string loadedPath,
                out string primaryError);

            Assert.True(loaded, primaryError);
            Assert.Equal("Version 2", recovered.Name);
            Assert.NotEqual(Path.GetFullPath(path), loadedPath);
            Assert.EndsWith(".bak", loadedPath, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(primaryError));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Save_UsesCurrentSchemaAndLeavesNoTemporaryFile()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "atomic" + LeagueStore.Extension);
        try
        {
            var league = new LeagueFile { SaveSchemaVersion = 0, Name = "Atomic Dynasty" };

            LeagueStore.Save(path, league);

            Assert.Equal(LeagueStore.CurrentSaveSchemaVersion, league.SaveSchemaVersion);
            Assert.True(File.Exists(path));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
            Assert.Equal("Atomic Dynasty", LeagueStore.Load(path).Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Save_RotatesBackupsAndKeepsNewestRecoverableState()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "rotation" + LeagueStore.Extension);
        try
        {
            var league = new LeagueFile();
            for (int version = 1; version <= 15; version++)
            {
                league.Name = "Version " + version;
                LeagueStore.Save(path, league);
                Thread.Sleep(2);
            }

            var backups = LeagueStore.GetBackups(path);

            Assert.Equal(12, backups.Count);
            Assert.All(backups, backup => Assert.True(backup.IsValid, backup.ErrorMessage));
            Assert.True(LeagueStore.TryLoad(backups[0].Path, out LeagueFile newestBackup, out string error), error);
            Assert.Equal("Version 14", newestBackup.Name);
            Assert.DoesNotContain(backups, backup =>
                LeagueStore.TryLoad(backup.Path, out LeagueFile old, out _) && old.Name == "Version 1");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void RecoveryListOrdersNewestBackupFirst()
    {
        string directory = NewTestDirectory();
        string path = Path.Combine(directory, "ordered" + LeagueStore.Extension);
        try
        {
            var league = new LeagueFile { Name = "First" };
            LeagueStore.Save(path, league);
            Thread.Sleep(5);
            league.Name = "Second";
            LeagueStore.Save(path, league);
            Thread.Sleep(5);
            league.Name = "Third";
            LeagueStore.Save(path, league);

            var backups = LeagueStore.GetBackups(path);

            Assert.True(backups.Count >= 3);
            Assert.Equal(backups.OrderByDescending(backup => backup.SavedAt).Select(backup => backup.Path),
                backups.Select(backup => backup.Path));
            Assert.True(LeagueStore.TryLoad(backups[0].Path, out LeagueFile newest, out string error), error);
            Assert.Equal("Second", newest.Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string NewTestDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-LeagueStoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
