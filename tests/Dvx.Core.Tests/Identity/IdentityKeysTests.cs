using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dvx.Core.Identity;

namespace Dvx.Core.Tests.Identity;

[TestClass]
public sealed class IdentityKeysTests
{
    [TestMethod]
    public void StepKey_IgnoresMessageAndEntityCase() =>
        Assert.AreEqual(
            IdentityKeys.Step("Contoso.Plugins", "Contoso.Plugins.AccountUpdatePreOperation", "update", "account", 20),
            IdentityKeys.Step("Contoso.Plugins", "Contoso.Plugins.AccountUpdatePreOperation", "Update", "Account", 20));

    [TestMethod]
    public void TypeKey_IsCaseSensitive() =>
        Assert.AreNotEqual(IdentityKeys.PluginType("A", "N.X"), IdentityKeys.PluginType("A", "n.x"));

    [TestMethod]
    public void CustomApiKey_IsLowercased() =>
        Assert.AreEqual("api:contoso_calculatediscount", IdentityKeys.CustomApi("contoso_CalculateDiscount"));

    [TestMethod]
    public void ImageKey_PostSuffix() =>
        Assert.AreEqual("img:step:A/T|m|e|20|post", IdentityKeys.Image("step:A/T|m|e|20", pre: false));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("none")]
    [DataRow("None")]
    [DataRow("NONE")]
    public void Step_WithoutEntity_Throws(string? entity) =>
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.Step("A", "T", "m", entity!, 20));

    [TestMethod]
    public void EmptyArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.Assembly(""));
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.PluginType("A", ""));
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.Step("A", "T", "", "e", 20));
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.CustomApi(""));
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.RequestParameter("a", ""));
        Assert.ThrowsExactly<ArgumentException>(() => IdentityKeys.ResponseProperty("", "p"));
    }
}
