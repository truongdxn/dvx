using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dvx.Core.Identity;

namespace Dvx.Core.Tests.Identity;

[TestClass]
public sealed class GoldenIdTests
{
    private const string Asm = "Contoso.Plugins";
    private const string Type = "Contoso.Plugins.AccountUpdatePreOperation";

    [TestMethod]
    [DataRow("asm:Contoso.Plugins", "5a4ef0f0-f377-578d-9bed-d4d804d84373")]
    [DataRow("type:Contoso.Plugins/Contoso.Plugins.AccountUpdatePreOperation", "3377f98f-ce1f-5677-89dd-468a04ee35f5")]
    [DataRow("step:Contoso.Plugins/Contoso.Plugins.AccountUpdatePreOperation|update|account|20", "cc989d30-229d-539f-90ee-de3f772b22ba")]
    [DataRow("img:step:Contoso.Plugins/Contoso.Plugins.AccountUpdatePreOperation|update|account|20|pre", "4f67c302-d632-5154-b8c7-9596fbbedf3b")]
    [DataRow("step:Contoso.Plugins/Contoso.Plugins.AccountUpdatePreOperation|update|account|40", "56f18d74-0cbc-55a9-979d-a30c0cc3ac89")]
    [DataRow("api:contoso_calculatediscount", "ef1d530a-0210-5be7-b33e-ccdf4d837aa6")]
    [DataRow("apireq:contoso_calculatediscount|contoso_amount", "1887102e-3bb7-5dea-97ad-7618660c2f99")]
    [DataRow("apiresp:contoso_calculatediscount|contoso_discount", "eb93e695-8164-59c4-b6bf-8be321c7bd5e")]
    public void Uuid5_MatchesGolden(string key, string expected) =>
        Assert.AreEqual(Guid.Parse(expected), Uuid5.Create(DvxNamespace.Value, key));

    [TestMethod]
    public void IdentityKeys_ProduceGoldenIds()
    {
        string step20 = IdentityKeys.Step(Asm, Type, "Update", "Account", 20);
        Check("5a4ef0f0-f377-578d-9bed-d4d804d84373", IdentityKeys.Assembly(Asm));
        Check("3377f98f-ce1f-5677-89dd-468a04ee35f5", IdentityKeys.PluginType(Asm, Type));
        Check("cc989d30-229d-539f-90ee-de3f772b22ba", step20);
        Check("4f67c302-d632-5154-b8c7-9596fbbedf3b", IdentityKeys.Image(step20, pre: true));
        Check("56f18d74-0cbc-55a9-979d-a30c0cc3ac89", IdentityKeys.Step(Asm, Type, "update", "account", 40));
        Check("ef1d530a-0210-5be7-b33e-ccdf4d837aa6", IdentityKeys.CustomApi("contoso_CalculateDiscount"));
        Check("1887102e-3bb7-5dea-97ad-7618660c2f99", IdentityKeys.RequestParameter("contoso_calculatediscount", "Contoso_Amount"));
        Check("eb93e695-8164-59c4-b6bf-8be321c7bd5e", IdentityKeys.ResponseProperty("contoso_calculatediscount", "Contoso_Discount"));
    }

    private static void Check(string expected, string key) =>
        Assert.AreEqual(Guid.Parse(expected), Uuid5.Create(DvxNamespace.Value, key));

    [TestMethod]
    public void Uuid5_NullName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Uuid5.Create(DvxNamespace.Value, null!));
}
