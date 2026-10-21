using System;
using System.Collections.Generic;
using System.Linq;
using Dvx.Core.Reading;
using Dvx.Core.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.State;

/// <summary>Non-identity changes must never change a component ID (SPEC §7).</summary>
[TestClass]
public class DesiredStateKeepIdTests
{
    private static readonly string[] Columns = { "name", "creditlimit" };

    private static PluginStepInfo Step(
        int rank = 1,
        IReadOnlyList<string>? filtering = null,
        IReadOnlyList<string>? pre = null,
        IReadOnlyList<string>? post = null,
        string? description = null,
        string? configuration = null) =>
        new("Update", "account", 20, 0, filtering, pre, post, rank, description, false, configuration);

    private static DesiredState Build(PluginStepInfo step) =>
        new DesiredStateBuilder().Build(new AssemblyInfo("Contoso.Plugins", new Version(1, 0, 0, 0), "neutral", "", "1.0.0",
            new[] { new PluginTypeInfo("Contoso.Plugins.AccountUpdatePreOperation", null, new[] { step }, null, true) },
            Array.Empty<EntityConstantsInfo>()));

    private static string[] Ids(DesiredState state) => state.Components.Select(c => c.Kind + "|" + c.Key + "|" + c.Id).ToArray();

    private static void AssertSameIds(PluginStepInfo before, PluginStepInfo after) =>
        CollectionAssert.AreEqual(Ids(Build(before)), Ids(Build(after)));

    [TestMethod]
    public void Rank_Change_KeepsAllIds() =>
        AssertSameIds(Step(rank: 1, pre: Columns), Step(rank: 9, pre: Columns));

    [TestMethod]
    public void FilteringAttributes_Change_KeepsAllIds() =>
        AssertSameIds(Step(filtering: new[] { "name" }, pre: Columns), Step(filtering: new[] { "name", "creditlimit", "x" }, pre: Columns));

    [TestMethod]
    public void Description_Change_KeepsAllIds() =>
        AssertSameIds(Step(description: "a", pre: Columns), Step(description: "b", pre: Columns));

    [TestMethod]
    public void UnsecureConfiguration_Change_KeepsAllIds() =>
        AssertSameIds(Step(configuration: null, pre: Columns), Step(configuration: "{\"k\":1}", pre: Columns));

    [TestMethod]
    public void ImageColumns_Change_KeepsAllIds() =>
        AssertSameIds(
            Step(pre: new[] { "name" }, post: new[] { "name" }),
            Step(pre: new[] { "name", "creditlimit" }, post: new[] { "*" }));
}
