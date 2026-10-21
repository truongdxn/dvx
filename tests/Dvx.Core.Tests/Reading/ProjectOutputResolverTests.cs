using System;
using System.IO;
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
}
