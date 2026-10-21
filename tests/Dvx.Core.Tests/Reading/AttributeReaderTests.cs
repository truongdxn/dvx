using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Dvx.Core.Reading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.Reading;

[TestClass]
public class AttributeReaderTests
{
    private static string s_dll = "";
    private static string[] s_references = Array.Empty<string>();
    private static AssemblyInfo s_info = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        // The sample is built by the MSBuild ProjectReference of this test project; here we only
        // ask MSBuild where it is and which references it has (no nested build).
        ProjectOutput output = ProjectOutputResolver.Resolve(FindSampleProject(), build: false);
        s_dll = output.TargetPath;
        s_references = output.ReferencePaths.ToArray();
        s_info = new AttributeReader().Read(s_dll, s_references);
    }

    private static string FindSampleProject()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "tests", "samples", "Sample.Plugins", "Sample.Plugins.csproj");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("Sample.Plugins.csproj not found above " + AppContext.BaseDirectory);
    }

    private static PluginTypeInfo Type(string name) => s_info.Types.Single(t => t.FullName == "Contoso.Plugins." + name);

    [TestMethod]
    public void Read_AssemblyIdentity()
    {
        Assert.AreEqual("Contoso.Plugins", s_info.Name);
        Assert.AreEqual(new Version(1, 0, 0, 0), s_info.Version);
        Assert.AreEqual("neutral", s_info.Culture);
        Assert.AreEqual("", s_info.PublicKeyToken);
    }

    [TestMethod]
    public void Read_Step_ReturnsAllArguments()
    {
        PluginTypeInfo type = Type("AccountUpdatePreOperation");
        PluginStepInfo step = type.Steps.Single();
        Assert.IsTrue(type.IsEntityPlugin);
        Assert.IsNull(type.CustomApi);
        Assert.IsNull(type.SourceFile);
        Assert.AreEqual("Update", step.Message);
        Assert.AreEqual("account", step.PrimaryEntity);
        Assert.AreEqual(20, step.Stage);
        Assert.AreEqual(0, step.Mode);
        CollectionAssert.AreEqual(new[] { "name", "creditlimit" }, step.FilteringAttributes!.ToArray());
        CollectionAssert.AreEqual(new[] { "name", "creditlimit" }, step.PreImageAttributes!.ToArray());
        Assert.IsNull(step.PostImageAttributes);
        Assert.AreEqual(1, step.Rank);
        Assert.AreEqual("Validate account changes", step.Description);
        Assert.IsFalse(step.AsyncAutoDelete);
        Assert.IsNull(step.UnsecureConfiguration);
    }

    [TestMethod]
    public void Read_Step_AppliesRankDefaultAndKeepsAbsentArraysNull()
    {
        PluginStepInfo step = Type("ContactCreatePostOperation").Steps.Single(s => s.Message == "Create");
        Assert.AreEqual(1, step.Rank);
        Assert.AreEqual(40, step.Stage);
        Assert.AreEqual(1, step.Mode);
        Assert.IsNull(step.FilteringAttributes);
        Assert.IsNull(step.PreImageAttributes);
        Assert.IsNull(step.PostImageAttributes);
        Assert.IsNull(step.Description);
    }

    [TestMethod]
    public void Read_Step_DistinguishesExplicitNullFromEmptyArray()
    {
        PluginStepInfo step = Type("ContactCreatePostOperation").Steps.Single(s => s.Message == "Delete");
        Assert.AreEqual(7, step.Rank);
        Assert.IsNull(step.PreImageAttributes);
        Assert.IsNotNull(step.PostImageAttributes);
        Assert.AreEqual(0, step.PostImageAttributes!.Count);
    }

    [TestMethod]
    public void Read_Steps_AreOrderedByMessageEntityStage()
    {
        PluginTypeInfo type = Type("ContactCreatePostOperation");
        CollectionAssert.AreEqual(new[] { "Create", "Delete" }, type.Steps.Select(s => s.Message).ToArray());
        CollectionAssert.AreEqual(
            s_info.Types.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            s_info.Types.Select(t => t.FullName).ToArray());
    }

    [TestMethod]
    public void Read_CustomApi_ReturnsParametersWithDirection()
    {
        PluginTypeInfo type = Type("CalculateDiscountApi");
        Assert.IsFalse(type.IsEntityPlugin);
        Assert.AreEqual(0, type.Steps.Count);
        CustomApiInfo api = type.CustomApi!;
        Assert.AreEqual("contoso_CalculateDiscount", api.UniqueName);
        Assert.AreEqual(0, api.BindingType);
        Assert.IsNull(api.BoundEntityLogicalName);
        Assert.IsFalse(api.IsFunction);
        Assert.IsFalse(api.IsPrivate);
        Assert.AreEqual(0, api.AllowedCustomProcessingStepType);
        Assert.AreEqual("prvContosoCalculateDiscount", api.ExecutePrivilegeName);
        Assert.AreEqual("Calculate Discount", api.DisplayName);

        Assert.AreEqual(3, api.Parameters.Count);
        CustomApiParamInfo amount = api.Parameters.Single(p => p.UniqueName == "contoso_Amount");
        Assert.IsTrue(amount.IsRequest);
        Assert.AreEqual(8, amount.Type); // Money
        Assert.IsFalse(amount.IsOptional);
        CustomApiParamInfo customer = api.Parameters.Single(p => p.UniqueName == "contoso_CustomerId");
        Assert.IsTrue(customer.IsRequest);
        Assert.AreEqual(5, customer.Type); // EntityReference
        Assert.IsTrue(customer.IsOptional);
        CustomApiParamInfo discount = api.Parameters.Single(p => p.UniqueName == "contoso_Discount");
        Assert.IsFalse(discount.IsRequest);
        Assert.AreEqual(2, discount.Type); // Decimal
    }

    [TestMethod]
    public void Read_SdkVersion_FromDvxSdkInfo()
    {
        Assert.AreEqual("0.1.0", s_info.SdkVersion);
    }

    [TestMethod]
    public void Read_EntityConstants_FromFieldsClass()
    {
        EntityConstantsInfo account = s_info.Entities.Single();
        Assert.AreEqual("Contoso.Plugins.Account", account.ClassFullName);
        Assert.AreEqual("account", account.LogicalName);
        CollectionAssert.AreEqual(
            new[] { ("CreditLimit", "creditlimit"), ("Name", "name") },
            account.Fields.ToArray());
    }

    [TestMethod]
    public void Read_MissingReference_ThrowsDvx180()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dvx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string copy = Path.Combine(dir, "Contoso.Plugins.dll");
            File.Copy(s_dll, copy);
            string[] onlyCore = s_references.Where(p => Path.GetFileName(p) == "mscorlib.dll").ToArray();
            Assert.AreEqual(1, onlyCore.Length);

            DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(
                () => new AttributeReader().Read(copy, onlyCore));
            Assert.AreEqual("DVX180", ex.Code);
            StringAssert.Contains(ex.Message, "Microsoft.Xrm.Sdk");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Read_DoesNotLoadIntoDefaultContext()
    {
        Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Contoso.Plugins"));
        foreach (AssemblyLoadContext context in AssemblyLoadContext.All)
        {
            Assert.IsFalse(context.Assemblies.Any(a => a.GetName().Name == "Contoso.Plugins"),
                "Loaded into context '" + context.Name + "'.");
        }
    }
}
