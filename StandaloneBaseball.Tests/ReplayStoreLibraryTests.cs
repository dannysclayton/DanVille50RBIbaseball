using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class ReplayStoreLibraryTests
{
    [Fact]
    public void DefaultReplayFolder_UsesCurrentUsersLocalApplicationData()
    {
        string localData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string replayFolder = Path.GetFullPath(ReplayStore.DefaultReplayFolder);

        Assert.StartsWith(localData, replayFolder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.DirectorySeparatorChar + "Desktop" + Path.DirectorySeparatorChar,
            replayFolder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportToLibrary_CopiesReadableReplayAndKeepsSource()
    {
        string root = TemporaryDirectory();
        try
        {
            string sourceFolder = Directory.CreateDirectory(Path.Combine(root, "incoming")).FullName;
            string library = Path.Combine(root, "library");
            string source = WriteSnapshotReplay(sourceFolder, "game.json");

            string imported = ReplayStore.ImportToLibrary(source, library);

            Assert.True(File.Exists(source));
            Assert.True(File.Exists(imported));
            Assert.EndsWith(ReplayStore.Extension, imported, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.GetFullPath(library), Path.GetDirectoryName(imported));
            Assert.Equal("Away", ReplayStore.Load(imported).Teams.Away.TeamName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportToLibrary_PreservesBothFilesWhenNamesMatch()
    {
        string root = TemporaryDirectory();
        try
        {
            string sourceFolder = Directory.CreateDirectory(Path.Combine(root, "incoming")).FullName;
            string library = Path.Combine(root, "library");
            string source = WriteSnapshotReplay(sourceFolder, "game.json");

            string first = ReplayStore.ImportToLibrary(source, library);
            string second = ReplayStore.ImportToLibrary(source, library);

            Assert.NotEqual(first, second);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
            Assert.Equal(2, ReplayStore.LibraryReplayFiles(library).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportToLibrary_RejectsMalformedJsonWithoutCopyingIt()
    {
        string root = TemporaryDirectory();
        try
        {
            string source = Path.Combine(root, "broken.json");
            string library = Path.Combine(root, "library");
            File.WriteAllText(source, "{ not valid json");

            Assert.ThrowsAny<Exception>(() => ReplayStore.ImportToLibrary(source, library));
            Assert.Empty(ReplayStore.LibraryReplayFiles(library));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportToLibrary_RejectsUnrelatedJsonWithoutCopyingIt()
    {
        string root = TemporaryDirectory();
        try
        {
            string source = Path.Combine(root, "unrelated.json");
            string library = Path.Combine(root, "library");
            File.WriteAllText(source, "{\"name\":\"not a replay\",\"items\":[1,2,3]}");

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ReplayStore.ImportToLibrary(source, library));

            Assert.Contains("recognizable replay data", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(ReplayStore.LibraryReplayFiles(library));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteLibraryReplay_RefusesFilesOutsideManagedFolder()
    {
        string root = TemporaryDirectory();
        try
        {
            string library = Directory.CreateDirectory(Path.Combine(root, "library")).FullName;
            string outside = WriteSnapshotReplay(root, "outside.json");

            Assert.Throws<InvalidOperationException>(() => ReplayStore.DeleteLibraryReplay(outside, library));
            Assert.True(File.Exists(outside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PackagedTemplate_IsAnExactReplayAndCanBeSavedByUser()
    {
        string root = TemporaryDirectory();
        try
        {
            string saved = Path.Combine(root, "My Replay Template" + ReplayStore.Extension);

            ReplayStore.SaveBundledTemplate(saved);
            ReplayFile replay = ReplayStore.Load(saved);

            Assert.True(File.Exists(saved));
            Assert.True(File.Exists(Path.Combine(root, "ExactReplaySchema.md")));
            Assert.True(replay.IsExact, string.Join(Environment.NewLine, replay.ReplayIssues));
            Assert.NotEmpty(replay.Events);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DansRBI-ReplayLibraryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteSnapshotReplay(string folder, string fileName)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        File.WriteAllText(path,
            "{\"replay_schema_version\":1,\"game\":{\"game_id\":\"game-1\"}," +
            "\"teams\":{\"away\":{\"team_name\":\"Away\"},\"home\":{\"team_name\":\"Home\"}}}");
        return path;
    }
}
