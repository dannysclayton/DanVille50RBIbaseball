using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class UserDataPathsTests
{
    [Fact]
    public void EditableGlobalPaths_UseCurrentUsersLocalApplicationData()
    {
        string localData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Assert.StartsWith(localData, Path.GetFullPath(UserDataPaths.RootDirectory), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(UserDataPaths.RootDirectory, UserDataPaths.SchoolsCsvPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(UserDataPaths.RootDirectory, UserDataPaths.LeagueCutsceneDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(UserDataPaths.RootDirectory, UserDataPaths.TeamMusicPlaylistDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UserDataPaths.SchoolsCsvPath, SchoolTeamCsvCatalog.RuntimeSchoolsCsvPath);
    }

    [Fact]
    public void EnsureSchoolsCsv_SeedsOnceAndPreservesUserEdits()
    {
        string root = TemporaryDirectory();
        try
        {
            string packageRoot = Path.Combine(root, "package");
            string userRoot = Path.Combine(root, "user");
            string packagedCsv = Path.Combine(packageRoot, "Assets", "Data", "schools.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(packagedCsv)!);
            File.WriteAllText(packagedCsv, "name,mascot\nSeed School,Seed Mascot\n");

            string userCsv = UserDataPaths.EnsureSchoolsCsv(userRoot, packageRoot);
            Assert.Equal(File.ReadAllText(packagedCsv), File.ReadAllText(userCsv));

            File.WriteAllText(userCsv, "name,mascot\nUser School,User Mascot\n");
            UserDataPaths.EnsureSchoolsCsv(userRoot, packageRoot);

            Assert.Contains("User School", File.ReadAllText(userCsv));
            Assert.Contains("Seed School", File.ReadAllText(packagedCsv));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureSeededDirectory_CopiesMissingFilesWithoutOverwritingUserContent()
    {
        string root = TemporaryDirectory();
        try
        {
            string seed = Path.Combine(root, "package", "Assets", "Cutscenes");
            string target = Path.Combine(root, "user", "Global Assets", "Cutscenes");
            Directory.CreateDirectory(Path.Combine(seed, "Pregame"));
            File.WriteAllText(Path.Combine(seed, "Pregame", "intro.txt"), "packaged");

            UserDataPaths.EnsureSeededDirectory(target, seed);
            string userFile = Path.Combine(target, "Pregame", "intro.txt");
            Assert.Equal("packaged", File.ReadAllText(userFile));

            File.WriteAllText(userFile, "user edited");
            File.WriteAllText(Path.Combine(seed, "Pregame", "new.txt"), "new packaged file");
            UserDataPaths.EnsureSeededDirectory(target, seed);

            Assert.Equal("user edited", File.ReadAllText(userFile));
            Assert.Equal("new packaged file", File.ReadAllText(Path.Combine(target, "Pregame", "new.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DansRBI-UserDataTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
