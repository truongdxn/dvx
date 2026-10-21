using System;
using System.IO;
using System.Linq;
using Dvx.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Cli.Tests;

[TestClass]
public class DescribeCommandTests
{
    private static string FindRepoFile(params string[] parts)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }

    private static (int Exit, string Out, string Err, string Work) Run(params string[] args)
    {
        string work = Path.Combine(Path.GetTempPath(), "dvx-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int exit = Program.Run(args, stdout, stderr, work);
        return (exit, stdout.ToString(), stderr.ToString(), work);
    }

    private static string ReadSnapshot(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", name));

    [TestMethod]
    public void Describe_Json_MatchesSnapshot()
    {
        string project = FindRepoFile("tests", "samples", "Sample.Plugins", "Sample.Plugins.csproj");
        var (exit, output, _, work) = Run("describe", "--project", project, "--json");
        try
        {
            Assert.AreEqual(0, exit);
            Assert.AreEqual(ReadSnapshot("describe.json"), output);
            Assert.IsTrue(Directory.GetFiles(Path.Combine(work, ".dvx", "logs"), "*.jsonl").Length == 1);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [TestMethod]
    public void Describe_UnknownProject_Exit2()
    {
        var (exit, output, _, work) = Run("describe", "--project", "does/not/exist.csproj", "--json");
        try
        {
            Assert.AreEqual(2, exit);
            Assert.AreEqual(ReadSnapshot("describe-unknown-project.json"), output);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [TestMethod]
    public void Describe_MissingProjectOption_Exit2()
    {
        var (exit, output, _, work) = Run("describe", "--json");
        try
        {
            Assert.AreEqual(2, exit);
            StringAssert.Contains(output, "\"DVX001\"");
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [TestMethod]
    public void Describe_Text_PrintsKindIdKey()
    {
        string project = FindRepoFile("tests", "samples", "Sample.Plugins", "Sample.Plugins.csproj");
        var (exit, output, _, work) = Run("describe", "--project", project);
        try
        {
            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "5a4ef0f0-f377-578d-9bed-d4d804d84373  asm:Contoso.Plugins");
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [TestMethod]
    public void Describe_LogsOptionNamesOnly()
    {
        var (_, _, _, work) = Run("describe", "--project", "secret-name.csproj", "--json");
        try
        {
            string log = File.ReadAllText(Directory.GetFiles(Path.Combine(work, ".dvx", "logs"), "*.jsonl").Single());
            StringAssert.Contains(log, "\"--project\"");
            StringAssert.Contains(log, "\"--json\"");
            Assert.IsFalse(log.Contains("secret-name", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}
