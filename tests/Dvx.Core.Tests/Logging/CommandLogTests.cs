using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dvx.Core.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.Logging;

[TestClass]
public class CommandLogTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dvx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [TestMethod]
    public void CommandLog_WritesOptionNamesOnly()
    {
        string dir = NewTempDir();
        try
        {
            // The caller passes names only; the value "dev" must never reach the file.
            new CommandLog().Append(dir, "describe", new[] { "--env" }, 0, new[] { "DVX181" }, TimeSpan.FromMilliseconds(12));

            string file = Directory.GetFiles(Path.Combine(dir, ".dvx", "logs"), "*.jsonl").Single();
            StringAssert.Matches(Path.GetFileName(file), new Regex(@"^\d{4}-\d{2}-\d{2}\.jsonl$"));
            string[] lines = File.ReadAllLines(file);
            Assert.AreEqual(1, lines.Length);
            StringAssert.Contains(lines[0], "\"--env\"");
            Assert.IsFalse(lines[0].Contains("dev", StringComparison.Ordinal));
            StringAssert.Contains(lines[0], "\"command\":\"describe\"");
            StringAssert.Contains(lines[0], "\"exitCode\":0");
            StringAssert.Contains(lines[0], "\"DVX181\"");
            StringAssert.Contains(lines[0], "\"durationMs\":12");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void FindRepoRoot_WalksUpToMarker()
    {
        string dir = NewTempDir();
        try
        {
            string nested = Path.Combine(dir, "a", "b");
            Directory.CreateDirectory(nested);
            Assert.IsNull(CommandLog.FindRepoRoot(nested));
            File.WriteAllText(Path.Combine(dir, "dvx.repo.json"), "{}");
            Assert.AreEqual(dir, CommandLog.FindRepoRoot(nested));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
