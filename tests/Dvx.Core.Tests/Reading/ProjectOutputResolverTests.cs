using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dvx.Core.Reading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.Reading;

[TestClass]
public class ProjectOutputResolverTests
{
    [TestMethod]
    public void Resolve_MissingProject_ThrowsDvx181()
    {
        string missing = Path.Combine(Path.GetTempPath(), "dvx-missing-" + Guid.NewGuid().ToString("N"), "None.csproj");
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(() => ProjectOutputResolver.Resolve(missing, build: true));
        Assert.AreEqual("DVX181", ex.Code);
        StringAssert.Contains(ex.Message, "does not exist");
    }

    [TestMethod]
    public void Resolve_Timeout_KillsProcessAndThrowsDvx181()
    {
        string? dir = AppContext.BaseDirectory;
        string project = "";
        while (dir != null && !File.Exists(project = Path.Combine(dir, "tests", "samples", "Sample.Plugins", "Sample.Plugins.csproj")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.IsNotNull(dir);
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(
            () => ProjectOutputResolver.Resolve(project, build: false, TimeSpan.FromMilliseconds(1)));
        Assert.AreEqual("DVX181", ex.Code);
        StringAssert.Contains(ex.Message, "did not finish");
    }

    [TestMethod]
    public void BuildStartInfo_Build_RestoresAndDisablesNodeReuse()
    {
        var info = ProjectOutputResolver.BuildStartInfo("/x/P.csproj", build: true, "dotnet");
        string[] args = info.ArgumentList.ToArray();
        CollectionAssert.Contains(args, "-restore");
        CollectionAssert.Contains(args, "-nodeReuse:false");
        CollectionAssert.Contains(args, "-t:Build");
    }

    [TestMethod]
    public void BuildStartInfo_NoBuild_DoesNotRestore()
    {
        var info = ProjectOutputResolver.BuildStartInfo("/x/P.csproj", build: false, "dotnet");
        string[] args = info.ArgumentList.ToArray();
        CollectionAssert.DoesNotContain(args, "-restore");
        CollectionAssert.Contains(args, "-nodeReuse:false");
        CollectionAssert.Contains(args, "-t:ResolveAssemblyReferences");
    }

    [TestMethod]
    public void BuildStartInfo_RemovesInheritedMsBuildEnvironment()
    {
        string[] names = { "MSBuildExtensionsPath", "MSBUILD_EXE_PATH", "MSBuildSDKsPath", "MSBuildLoadMicrosoftTargetsReadOnly", "msbuildcustom" };
        var saved = names.ToDictionary(n => n, n => Environment.GetEnvironmentVariable(n));
        try
        {
            foreach (string n in names) Environment.SetEnvironmentVariable(n, "/stale");
            var info = ProjectOutputResolver.BuildStartInfo("/x/P.csproj", build: true, "dotnet");
            foreach (string key in info.Environment.Keys)
            {
                Assert.IsFalse(key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase), key);
            }
        }
        finally
        {
            foreach (var pair in saved) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [TestMethod]
    public void Resolve_BuildOnFreshCopyWithoutObj_RestoresAndBuilds()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Directory.Packages.props"))) root = Path.GetDirectoryName(root);
        Assert.IsNotNull(root);

        string copy = Path.Combine(Path.GetTempPath(), "dvx-fresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string file in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "nuget.config", "global.json" })
            {
                if (File.Exists(Path.Combine(root, file))) { Directory.CreateDirectory(copy); File.Copy(Path.Combine(root, file), Path.Combine(copy, file), overwrite: true); }
            }

            CopyTree(Path.Combine(root, "src", "Dvx.Plugin.Sdk"), Path.Combine(copy, "src", "Dvx.Plugin.Sdk"));
            CopyTree(Path.Combine(root, "tests", "samples", "Sample.Plugins"), Path.Combine(copy, "tests", "samples", "Sample.Plugins"));

            ProjectOutput output = ProjectOutputResolver.Resolve(Path.Combine(copy, "tests", "samples", "Sample.Plugins", "Sample.Plugins.csproj"), build: true);
            Assert.IsTrue(File.Exists(output.TargetPath), output.TargetPath);
            StringAssert.StartsWith(output.TargetPath, copy);
        }
        finally
        {
            try { Directory.Delete(copy, recursive: true); } catch (IOException) { }
        }
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(from, file);
            string first = relative.Split(Path.DirectorySeparatorChar)[0];
            if (first is "bin" or "obj") continue;
            string target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
