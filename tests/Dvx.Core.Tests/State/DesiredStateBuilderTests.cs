using System;
using System.Collections.Generic;
using System.Linq;
using Dvx.Core.Reading;
using Dvx.Core.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx.Core.Tests.State;

[TestClass]
public class DesiredStateBuilderTests
{
    private const string Type = "Contoso.Plugins.AccountUpdatePreOperation";

    private static PluginStepInfo Step(
        int stage = 20,
        int mode = 0,
        IReadOnlyList<string>? filtering = null,
        IReadOnlyList<string>? pre = null,
        IReadOnlyList<string>? post = null) =>
        new("Update", "account", stage, mode, filtering, pre, post, 1, "Validate account changes", false, null);

    private static AssemblyInfo Info(params PluginStepInfo[] steps) =>
        new("Contoso.Plugins", new Version(1, 0, 0, 0), "neutral", "", "1.0.0",
            new[] { new PluginTypeInfo(Type, null, steps, null, true) },
            Array.Empty<EntityConstantsInfo>());

    private static Component Single(DesiredState state, ComponentKind kind) => state.Components.Single(c => c.Kind == kind);

    [TestMethod]
    public void Build_StepAndImage_UseGoldenIds()
    {
        DesiredState state = new DesiredStateBuilder().Build(
            Info(Step(filtering: new[] { "name", "creditlimit" }, pre: new[] { "name", "creditlimit" })));

        Component type = Single(state, ComponentKind.PluginType);
        Component step = Single(state, ComponentKind.Step);
        Component image = Single(state, ComponentKind.Image);
        Assert.AreEqual(Guid.Parse("5a4ef0f0-f377-578d-9bed-d4d804d84373"), state.Assembly.Id);
        Assert.AreEqual(Guid.Parse("3377f98f-ce1f-5677-89dd-468a04ee35f5"), type.Id);
        Assert.AreEqual(Guid.Parse("cc989d30-229d-539f-90ee-de3f772b22ba"), step.Id);
        Assert.AreEqual(Guid.Parse("4f67c302-d632-5154-b8c7-9596fbbedf3b"), image.Id);
        Assert.AreEqual(step.Id, image.ParentId);
        Assert.AreEqual(type.Id, step.ParentId);
        Assert.AreEqual(state.Assembly.Id, type.ParentId);
        Assert.IsNull(state.Assembly.ParentId);
        Assert.AreEqual("creditlimit,name", step.Fields["filteringattributes"]);
        Assert.AreEqual("Contoso.Plugins.AccountUpdatePreOperation: Update of account", step.Fields["name"]);
        Assert.AreEqual("0", image.Fields["imagetype"]);
        Assert.AreEqual("PreImage", image.Fields["entityalias"]);
    }

    [TestMethod]
    public void Build_ModeChange_KeepsStepId()
    {
        var builder = new DesiredStateBuilder();
        Component sync = Single(builder.Build(Info(Step(mode: 0))), ComponentKind.Step);
        Component async = Single(builder.Build(Info(Step(mode: 1))), ComponentKind.Step);
        Assert.AreEqual(sync.Id, async.Id);
        Assert.AreEqual("0", sync.Fields["mode"]);
        Assert.AreEqual("1", async.Fields["mode"]);
    }

    [TestMethod]
    public void Build_StageChange_ChangesStepAndImageIds()
    {
        var builder = new DesiredStateBuilder();
        DesiredState pre = builder.Build(Info(Step(stage: 20, pre: new[] { "name" })));
        DesiredState post = builder.Build(Info(Step(stage: 40, pre: new[] { "name" })));
        Assert.AreEqual(Guid.Parse("56f18d74-0cbc-55a9-979d-a30c0cc3ac89"), Single(post, ComponentKind.Step).Id);
        Assert.AreNotEqual(Single(pre, ComponentKind.Step).Id, Single(post, ComponentKind.Step).Id);
        Assert.AreNotEqual(Single(pre, ComponentKind.Image).Id, Single(post, ComponentKind.Image).Id);
    }

    [TestMethod]
    public void Build_AllColumnsMarker_BecomesEmptyAttributes()
    {
        DesiredState state = new DesiredStateBuilder().Build(
            Info(Step(filtering: new[] { "*" }, pre: new[] { "*" }, post: new[] { "b", "a" })));
        Assert.AreEqual("", Single(state, ComponentKind.Step).Fields["filteringattributes"]);
        Component[] images = state.Components.Where(c => c.Kind == ComponentKind.Image).ToArray();
        Assert.AreEqual(2, images.Length);
        Assert.AreEqual("", images.Single(i => i.Fields["entityalias"] == "PreImage").Fields["attributes"]);
        Assert.AreEqual("a,b", images.Single(i => i.Fields["entityalias"] == "PostImage").Fields["attributes"]);
    }

    [TestMethod]
    public void Build_IsDeterministic_SameInputSameComponentsInSameOrder()
    {
        AssemblyInfo info = Info(Step(stage: 40, pre: new[] { "name" }), Step(stage: 20, post: new[] { "name" }));
        var builder = new DesiredStateBuilder();
        string[] first = builder.Build(info).Components.Select(c => c.Kind + "|" + c.Key + "|" + c.Id).ToArray();
        string[] second = builder.Build(info).Components.Select(c => c.Kind + "|" + c.Key + "|" + c.Id).ToArray();
        CollectionAssert.AreEqual(first, second);
        CollectionAssert.AreEqual(
            first.OrderBy(l => Enum.Parse<ComponentKind>(l.Split('|')[0])).ThenBy(l => l.Split('|')[1], StringComparer.Ordinal).ToArray(),
            first);
    }

    [TestMethod]
    public void Build_DuplicateIds_ThrowsInternalError()
    {
        // Same type/message/entity/stage, different mode: identical identity key.
        Assert.ThrowsExactly<DuplicateComponentIdException>(
            () => new DesiredStateBuilder().Build(Info(Step(mode: 0), Step(mode: 1))));
    }

    [TestMethod]
    [DataRow("none")]
    [DataRow("None")]
    public void Build_StepWithGlobalMessageEntityNone_ThrowsDvx184(string entity)
    {
        var step = new PluginStepInfo("Associate", entity, 20, 0, null, null, null, 1, null, false, null);
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(() => new DesiredStateBuilder().Build(Info(step)));
        Assert.AreEqual("DVX184", ex.Code);
        StringAssert.Contains(ex.Message, "global messages without a primary entity are not supported yet");
    }

    [TestMethod]
    public void Build_StepWithoutEntity_ThrowsReadException()
    {
        var step = new PluginStepInfo("Associate", "", 20, 0, null, null, null, 1, null, false, null);
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(() => new DesiredStateBuilder().Build(Info(step)));
        Assert.AreEqual("DVX184", ex.Code);
    }

    [TestMethod]
    public void Build_EmptyTypeName_ThrowsReadException()
    {
        var info = new AssemblyInfo("Contoso.Plugins", new Version(1, 0, 0, 0), "neutral", "", "1.0.0",
            new[] { new PluginTypeInfo("", null, Array.Empty<PluginStepInfo>(), null, true) },
            Array.Empty<EntityConstantsInfo>());
        DvxReadException ex = Assert.ThrowsExactly<DvxReadException>(() => new DesiredStateBuilder().Build(info));
        Assert.AreEqual("DVX184", ex.Code);
    }

    [TestMethod]
    public void ImmutableFields_MatchSpec()
    {
        CollectionAssert.AreEquivalent(
            new[] { "uniquename", "bindingtype", "boundentitylogicalname", "isfunction", "allowedcustomprocessingsteptype" },
            ComponentRules.ImmutableFields(ComponentKind.CustomApi).ToArray());
        Assert.AreEqual(0, ComponentRules.ImmutableFields(ComponentKind.Step).Count);
    }

    [TestMethod]
    public void Build_CustomApi_ParentsAndFields()
    {
        var api = new CustomApiInfo("contoso_CalculateDiscount", 0, null, false, false, 0, "prv", "Calculate Discount",
            new[]
            {
                new CustomApiParamInfo("contoso_Amount", 2, false, true),
                new CustomApiParamInfo("contoso_Discount", 3, false, false),
            });
        var info = new AssemblyInfo("Contoso.Plugins", new Version(1, 0, 0, 0), "neutral", "", "1.0.0",
            new[] { new PluginTypeInfo("Contoso.Plugins.CalculateDiscountApi", null, Array.Empty<PluginStepInfo>(), api, false) },
            Array.Empty<EntityConstantsInfo>());
        DesiredState state = new DesiredStateBuilder().Build(info);
        Component type = Single(state, ComponentKind.PluginType);
        Component apiComponent = Single(state, ComponentKind.CustomApi);
        Assert.AreEqual(type.Id, apiComponent.ParentId);
        Assert.AreEqual(Guid.Parse("ef1d530a-0210-5be7-b33e-ccdf4d837aa6"), apiComponent.Id);
        Assert.AreEqual(apiComponent.Id, Single(state, ComponentKind.RequestParameter).ParentId);
        Assert.AreEqual(Guid.Parse("1887102e-3bb7-5dea-97ad-7618660c2f99"), Single(state, ComponentKind.RequestParameter).Id);
        Assert.AreEqual(Guid.Parse("eb93e695-8164-59c4-b6bf-8be321c7bd5e"), Single(state, ComponentKind.ResponseProperty).Id);
        Assert.AreEqual("contoso_CalculateDiscount", apiComponent.Fields["uniquename"]);
        Assert.IsNull(apiComponent.Fields["boundentitylogicalname"]);
    }
}
