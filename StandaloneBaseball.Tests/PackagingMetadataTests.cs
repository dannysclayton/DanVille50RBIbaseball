using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class PackagingMetadataTests
{
    [Fact]
    public void ApplicationAssembly_UsesReleaseExecutableNameAndVersion()
    {
        var assembly = typeof(LeagueFile).Assembly;
        var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);

        Assert.Equal("DanVille50RBIbaseball", assembly.GetName().Name);
        Assert.Equal(new Version(1, 0, 0, 0), assembly.GetName().Version);
        Assert.Equal("1.0.0.0", fileVersion.FileVersion);
        Assert.Equal("1.0", fileVersion.ProductVersion);
        Assert.Equal("Dan's RBI Baseball 2026", fileVersion.FileDescription);
        Assert.Equal("DanVille50", fileVersion.CompanyName);
        Assert.Equal("Dan's RBI Baseball 2026", fileVersion.ProductName);
    }

    [Fact]
    public void Project_PublishesRuntimeAssetsBesideSingleFileExecutable()
    {
        string projectPath = FindProjectFile();
        XDocument project = XDocument.Load(projectPath);
        XElement assets = project.Descendants("None").Single(element =>
            string.Equals((string)element.Attribute("Update"), "Assets\\**\\*.*", StringComparison.OrdinalIgnoreCase));

        Assert.Null(assets.Attribute("Include"));
        Assert.Equal("PreserveNewest", assets.Element("CopyToOutputDirectory")?.Value);
        Assert.Equal("PreserveNewest", assets.Element("CopyToPublishDirectory")?.Value);
        Assert.Equal("true", assets.Element("ExcludeFromSingleFile")?.Value, ignoreCase: true);

        string profiles = Path.Combine(Path.GetDirectoryName(projectPath)!, "Properties", "PublishProfiles");
        foreach (string profileName in new[] { "PublicV1SingleFile.pubxml", "LocalV2SingleFile.pubxml" })
        {
            XDocument profile = XDocument.Load(Path.Combine(profiles, profileName));
            Assert.Equal("true", profile.Descendants("SelfContained").Single().Value, ignoreCase: true);
            Assert.Equal("true", profile.Descendants("PublishSingleFile").Single().Value, ignoreCase: true);
            Assert.Equal("false", profile.Descendants("PublishTrimmed").Single().Value, ignoreCase: true);
            Assert.Equal("win-x64", profile.Descendants("RuntimeIdentifier").Single().Value);
            Assert.Equal("true", profile.Descendants("Deterministic").Single().Value, ignoreCase: true);
            Assert.Equal("true", profile.Descendants("RestoreLockedMode").Single().Value, ignoreCase: true);
        }
    }

    [Fact]
    public void DistributionProfiles_SeparatePublicMediaFromUnchangedLocalVersionTwoAssets()
    {
        string projectPath = FindProjectFile();
        XDocument project = XDocument.Load(projectPath);
        XElement publicMedia = project.Descendants("None").Single(element =>
            ((string)element.Attribute("Update") ?? "").Contains("Assets\\**\\*.mp3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("DistributionChannel", (string)publicMedia.Attribute("Condition"));
        Assert.Contains("Public", (string)publicMedia.Attribute("Condition"));
        Assert.Equal("Never", publicMedia.Element("CopyToPublishDirectory")?.Value);

        string profiles = Path.Combine(Path.GetDirectoryName(projectPath)!, "Properties", "PublishProfiles");
        XDocument publicProfile = XDocument.Load(Path.Combine(profiles, "PublicV1SingleFile.pubxml"));
        XDocument localProfile = XDocument.Load(Path.Combine(profiles, "LocalV2SingleFile.pubxml"));

        Assert.Equal("Public", publicProfile.Descendants("DistributionChannel").Single().Value);
        Assert.Equal("1.0.0.0", publicProfile.Descendants("FileVersion").Single().Value);
        Assert.Contains("PUBLIC_RELEASE", publicProfile.Descendants("DefineConstants").Single().Value);
        Assert.Equal("Local", localProfile.Descendants("DistributionChannel").Single().Value);
        Assert.Equal("2.0.0.0", localProfile.Descendants("FileVersion").Single().Value);
        Assert.Equal("2.0 Local Only", localProfile.Descendants("InformationalVersion").Single().Value);
        Assert.Contains("LOCAL_ONLY_V2", localProfile.Descendants("DefineConstants").Single().Value);
    }

    [Fact]
    public void ReproducibleBuild_IsSdkPinnedLockedAndBranded()
    {
        string projectPath = FindProjectFile();
        string root = Directory.GetParent(Path.GetDirectoryName(projectPath)!)!.FullName;
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");

        Assert.Equal("8.0.422", sdk.GetProperty("version").GetString());
        Assert.Equal("disable", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());

        XDocument project = XDocument.Load(projectPath);
        Assert.Equal("true", project.Descendants("Deterministic").Single().Value, ignoreCase: true);
        Assert.Equal("true", project.Descendants("RestorePackagesWithLockFile").Single().Value, ignoreCase: true);
        Assert.Equal("true", project.Descendants("RestoreLockedMode").Single().Value, ignoreCase: true);
        Assert.Equal("Branding\\DansRBIBaseball.ico", project.Descendants("ApplicationIcon").Single().Value);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(projectPath)!, "Branding", "DansRBIBaseball.ico")));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json")));
        Assert.True(File.Exists(Path.Combine(root, "StandaloneBaseball.Tests", "packages.lock.json")));
    }

    [Fact]
    public void DistributionInstallersAndSigningPipelines_RemainSeparate()
    {
        string projectPath = FindProjectFile();
        string root = Directory.GetParent(Path.GetDirectoryName(projectPath)!)!.FullName;
        string publicInstaller = File.ReadAllText(Path.Combine(root, "packaging", "installer", "PublicV1.iss"));
        string localInstaller = File.ReadAllText(Path.Combine(root, "packaging", "installer", "LocalV2.iss"));
        string publicScript = File.ReadAllText(Path.Combine(root, "publish-public.ps1"));
        string localScript = File.ReadAllText(Path.Combine(root, "publish-local-v2.ps1"));

        Assert.Contains("5A0D488D-579A-49D8-BD72-1B2AF1688610", publicInstaller);
        Assert.Contains("B9271E06-CBA7-499B-A409-FC0861DEB213", localInstaller);
        Assert.DoesNotContain("B9271E06-CBA7-499B-A409-FC0861DEB213", publicInstaller);
        Assert.DoesNotContain("5A0D488D-579A-49D8-BD72-1B2AF1688610", localInstaller);
        Assert.Contains("PublicV1.iss", publicScript);
        Assert.Contains("LocalV2.iss", localScript);
        Assert.Contains("Invoke-AuthenticodeSigning.ps1", publicScript);
        Assert.Contains("Invoke-AuthenticodeSigning.ps1", localScript);
    }

    private static string FindProjectFile()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string direct = Path.Combine(directory.FullName, "StandaloneBaseball.csproj");
            if (File.Exists(direct))
                return direct;
            string sibling = Path.Combine(directory.FullName, "StandaloneBaseball", "StandaloneBaseball.csproj");
            if (File.Exists(sibling))
                return sibling;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate StandaloneBaseball.csproj from the test output directory.");
    }
}
