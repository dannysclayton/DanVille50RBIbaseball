using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class ExceptionHandlerTests
{
    [Fact]
    public void PublicReleaseHandler_WritesPublicVersionLogWithoutThrowing()
    {
        string root = TemporaryDirectory();
        try
        {
            string path = PublicReleaseExceptionHandler.WriteLog(
                new InvalidOperationException("public test failure"),
                "Public handler test",
                root);

            Assert.True(File.Exists(path));
            string text = File.ReadAllText(path);
            Assert.Contains("Public Version 1.0", text);
            Assert.Contains("Public handler test", text);
            Assert.Contains("public test failure", text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LocalV2Handler_WritesSeparateLocalVersionLogWithoutThrowing()
    {
        string root = TemporaryDirectory();
        try
        {
            string path = LocalV2ExceptionHandler.WriteLog(
                new InvalidOperationException("local test failure"),
                "Local handler test",
                root);

            Assert.True(File.Exists(path));
            string text = File.ReadAllText(path);
            Assert.Contains("Local-only Version 2.0", text);
            Assert.Contains("Local handler test", text);
            Assert.Contains("local test failure", text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DansRBI-ExceptionHandlerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
