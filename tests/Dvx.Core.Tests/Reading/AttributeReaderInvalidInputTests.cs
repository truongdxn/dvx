using System;
using System.IO;
using System.Linq;
using Dvx.Core.Reading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.Reading;

[TestClass]
public class AttributeReaderInvalidInputTests
{
    private static string FindProject(string name)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "tests", "samples", name, name + ".csproj");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(name + ".csproj not found above " + AppContext.BaseDirectory);
    }

    [TestMethod]
    public void Read_AbstractAndOpenGenericPluginTypes_ThrowDvx185()
    {
        ProjectOutput output = ProjectOutputResolver.Resolve(FindProject("Sample.Invalid"), build: false);
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(
            () => new AttributeReader().Read(output.TargetPath, output.ReferencePaths.ToArray()));
        Assert.AreEqual("DVX185", ex.Code);
        StringAssert.Contains(ex.Message, "plugin type cannot be instantiated by Dataverse");
        StringAssert.Contains(ex.Message, "Fabrikam.Invalid.AbstractStepPlugin");
        StringAssert.Contains(ex.Message, "Fabrikam.Invalid.GenericStepPlugin");
    }

    [TestMethod]
    public void Read_TextFileNamedDll_ThrowsDvx180()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dvx-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string dll = Path.Combine(dir, "Broken.dll");
            File.WriteAllText(dll, "this is not an assembly");
            DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(() => new AttributeReader().Read(dll, Array.Empty<string>()));
            Assert.AreEqual("DVX180", ex.Code);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Read_MissingDll_ThrowsDvx180()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dvx-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(
                () => new AttributeReader().Read(Path.Combine(dir, "Missing.dll"), Array.Empty<string>()));
            Assert.AreEqual("DVX180", ex.Code);
            StringAssert.Contains(ex.Message, "Missing.dll");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Read_MissingDirectory_ThrowsDvx180()
    {
        string dll = Path.Combine(Path.GetTempPath(), "dvx-nodir-" + Guid.NewGuid().ToString("N"), "X.dll");
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(() => new AttributeReader().Read(dll, Array.Empty<string>()));
        Assert.AreEqual("DVX180", ex.Code);
    }
}
