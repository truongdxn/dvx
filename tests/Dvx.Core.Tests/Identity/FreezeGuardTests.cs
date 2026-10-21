using System;
using System.Globalization;
using Dvx.Core.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.Identity;

/// <summary>Guards for the frozen identity scheme (SPEC §7): culture independence and assembly-name case sensitivity.</summary>
[TestClass]
public sealed class FreezeGuardTests
{
    private const string Asm = "Contoso.Plugins";
    private const string Type = "Contoso.Plugins.AccountUpdatePreOperation";

    private static void UnderCulture(string name, Action action)
    {
        CultureInfo savedCulture = CultureInfo.CurrentCulture;
        CultureInfo savedUi = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }

    private static Guid Id(string key) => Uuid5.Create(DvxNamespace.Value, key);

    private static string[] ComputeKeys()
    {
        string step = IdentityKeys.Step(Asm, Type, "Indexing", "INVOICE", 20);
        return new[]
        {
            step,
            IdentityKeys.Image(step, pre: true),
            IdentityKeys.CustomApi("contoso_IsInvoice"),
            IdentityKeys.RequestParameter("contoso_IsInvoice", "contoso_ItemId"),
            IdentityKeys.ResponseProperty("contoso_IsInvoice", "contoso_ItemId"),
        };
    }

    private static Guid[] ComputeIds() => new[]
    {
        Id(IdentityKeys.Step(Asm, Type, "Indexing", "INVOICE", 20)),
        Id(IdentityKeys.CustomApi("contoso_IsInvoice")),
        Id(IdentityKeys.RequestParameter("contoso_IsInvoice", "contoso_ItemId")),
    };

    [TestMethod]
    public void Keys_UnderTurkishCulture_UseInvariantLowercase()
    {
        string[] invariant = ComputeKeys();
        UnderCulture("tr-TR", () =>
        {
            // Sanity: the culture really lowercases capital I differently.
            Assert.AreEqual("ı", "I".ToLower(CultureInfo.CurrentCulture));
            CollectionAssert.AreEqual(invariant, ComputeKeys());
        });

        Assert.AreEqual("step:Contoso.Plugins/Contoso.Plugins.AccountUpdatePreOperation|indexing|invoice|20", invariant[0]);
        Assert.AreEqual("api:contoso_isinvoice", invariant[2]);
        Assert.AreEqual("apireq:contoso_isinvoice|contoso_itemid", invariant[3]);
        Assert.AreEqual("apiresp:contoso_isinvoice|contoso_itemid", invariant[4]);
    }

    [TestMethod]
    public void Ids_UnderTurkishCulture_EqualInvariantIds()
    {
        Guid[] invariant = ComputeIds();
        UnderCulture("tr-TR", () => CollectionAssert.AreEqual(invariant, ComputeIds()));
    }

    [TestMethod]
    public void Goldens_HoldUnderTurkishCulture() =>
        UnderCulture("tr-TR", () =>
        {
            Assert.AreEqual(Guid.Parse("cc989d30-229d-539f-90ee-de3f772b22ba"), Id(IdentityKeys.Step(Asm, Type, "Update", "Account", 20)));
            Assert.AreEqual(Guid.Parse("ef1d530a-0210-5be7-b33e-ccdf4d837aa6"), Id(IdentityKeys.CustomApi("contoso_CalculateDiscount")));
            Assert.AreEqual(Guid.Parse("1887102e-3bb7-5dea-97ad-7618660c2f99"), Id(IdentityKeys.RequestParameter("contoso_calculatediscount", "Contoso_Amount")));
        });

    [TestMethod]
    public void AssemblyNameCase_ChangesAssemblyTypeAndStepIds()
    {
        const string lower = "contoso.plugins";
        Assert.AreNotEqual(Id(IdentityKeys.Assembly(Asm)), Id(IdentityKeys.Assembly(lower)));
        Assert.AreNotEqual(Id(IdentityKeys.PluginType(Asm, Type)), Id(IdentityKeys.PluginType(lower, Type)));
        Assert.AreNotEqual(
            Id(IdentityKeys.Step(Asm, Type, "Update", "account", 20)),
            Id(IdentityKeys.Step(lower, Type, "Update", "account", 20)));
    }
}
