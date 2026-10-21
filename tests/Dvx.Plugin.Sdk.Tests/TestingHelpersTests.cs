using System;
using System.Collections.Generic;
using System.Linq;
using Dvx.Plugin.Sdk;
using Dvx.Testing;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dvx_Plugin_Sdk_Tests
{
    [TestClass]
    public class TestingHelpersTests
    {
        private sealed class CreateTaskFlow : FlowBase
        {
            public CreateTaskFlow(PluginRuntime r) : base(r) { }
            public override void Execute()
            {
                var task = new Entity("task");
                task["subject"] = "Follow up " + Runtime.Target.GetAttributeValue<string>("name");
                Runtime.UserService.Create(task);
            }
        }

        private sealed class AccountPlugin : EntityPluginBase
        {
            protected override IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime)
            {
                yield return new CreateTaskFlow(runtime);
            }
        }

        private sealed class CapturePlugin : IPlugin
        {
            private readonly Action<IPluginExecutionContext> _capture;
            public CapturePlugin(Action<IPluginExecutionContext> capture) { _capture = capture; }
            public void Execute(IServiceProvider sp)
            {
                _capture((IPluginExecutionContext)sp.GetService(typeof(IPluginExecutionContext)));
            }
        }

        [TestMethod]
        public void InMemory_CreateThenRetrieve_ReturnsRequestedColumnsOnly()
        {
            var svc = new InMemoryOrganizationService();
            var e = new Entity("account");
            e["name"] = "Contoso";
            e["city"] = "Oslo";
            var id = svc.Create(e);

            var r = svc.Retrieve("account", id, new ColumnSet("name", "missing"));

            Assert.AreEqual(id, r.Id);
            Assert.AreEqual("account", r.LogicalName);
            Assert.AreEqual("Contoso", r.GetAttributeValue<string>("name"));
            Assert.IsFalse(r.Contains("city"));
            Assert.IsFalse(r.Contains("missing"));
        }

        [TestMethod]
        public void InMemory_RetrieveMultiple_FiltersByEqualCondition()
        {
            var svc = new InMemoryOrganizationService();
            svc.Seed(Account("A", "Oslo"), Account("B", "Bergen"), Account("C", "Oslo"));
            var q = new QueryExpression("account") { ColumnSet = new ColumnSet("name") };
            q.Criteria.AddCondition("city", ConditionOperator.Equal, "Oslo");

            var names = svc.RetrieveMultiple(q).Entities.Select(x => x.GetAttributeValue<string>("name")).OrderBy(x => x).ToArray();

            CollectionAssert.AreEqual(new[] { "A", "C" }, names);
        }

        [TestMethod]
        public void InMemory_RetrieveMultiple_SupportsNotEqualNullAndNotNull()
        {
            var svc = new InMemoryOrganizationService();
            var noCity = new Entity("account");
            noCity["name"] = "N";
            svc.Seed(Account("A", "Oslo"), Account("B", "Bergen"), noCity);

            Assert.AreEqual(1, Query(svc, "city", ConditionOperator.NotEqual, "Oslo").Count());
            Assert.AreEqual(1, Query(svc, "city", ConditionOperator.Null).Count());
            Assert.AreEqual(2, Query(svc, "city", ConditionOperator.NotNull).Count());
        }

        [TestMethod]
        public void InMemory_RetrieveMultiple_UnsupportedOperatorThrowsNamingOperator()
        {
            var svc = new InMemoryOrganizationService();
            svc.Seed(Account("A", "Oslo"));
            var ex = Assert.ThrowsExactly<NotSupportedException>(
                () => Query(svc, "city", ConditionOperator.Like, "O%").ToList());
            StringAssert.Contains(ex.Message, "Like");
        }

        [TestMethod]
        public void InMemory_UnsupportedRequest_ThrowsNotSupportedWithRequestName()
        {
            var svc = new InMemoryOrganizationService();
            var ex = Assert.ThrowsExactly<NotSupportedException>(() => svc.Execute(new WinOpportunityRequest()));
            StringAssert.Contains(ex.Message, "WinOpportunity");
        }

        [TestMethod]
        public void InMemory_UpdateMergesAttributes_AndStoredRecordsAreIsolatedClones()
        {
            var svc = new InMemoryOrganizationService();
            var e = Account("A", "Oslo");
            e.Id = Guid.NewGuid();
            svc.Seed(e);
            e["name"] = "mutated";

            var patch = new Entity("account", e.Id);
            patch["city"] = "Bergen";
            svc.Update(patch);

            var got = svc.Get("account", e.Id);
            Assert.AreEqual("A", got.GetAttributeValue<string>("name"));
            Assert.AreEqual("Bergen", got.GetAttributeValue<string>("city"));
            got["name"] = "mutated again";
            Assert.AreEqual("A", svc.Get("account", e.Id).GetAttributeValue<string>("name"));
            Assert.IsNull(svc.Get("account", Guid.NewGuid()));
        }

        [TestMethod]
        public void Context_ExecutesEntityPlugin_AndCapturesTrace()
        {
            var target = new Entity("account", Guid.NewGuid());
            target["name"] = "Contoso";
            var ctx = PluginTestContext.For("Create", "account", Stage.PostOperation).WithTarget(target);

            ctx.Execute(new AccountPlugin());

            var tasks = ctx.Service.RetrieveMultiple(new QueryExpression("task") { ColumnSet = new ColumnSet(true) });
            Assert.AreEqual(1, tasks.Entities.Count);
            Assert.AreEqual("Follow up Contoso", tasks.Entities[0].GetAttributeValue<string>("subject"));
            CollectionAssert.Contains(ctx.Trace.ToList(), "CreateTaskFlow: start");
            CollectionAssert.Contains(ctx.Trace.ToList(), "CreateTaskFlow: end");
        }

        [TestMethod]
        public void Context_ExposesMessageStageDepthImagesAndInputs()
        {
            var target = new Entity("account", Guid.NewGuid());
            var pre = new Entity("account", target.Id);
            var post = new Entity("account", target.Id);
            var ctx = PluginTestContext.For("Update", "account", Stage.PreOperation)
                .WithTarget(target).WithPreImage(pre).WithPostImage(post).WithInput("Extra", 5).WithDepth(3);
            IPluginExecutionContext seen = null;

            ctx.Execute(new CapturePlugin(c => seen = c));

            Assert.AreEqual("Update", seen.MessageName);
            Assert.AreEqual("account", seen.PrimaryEntityName);
            Assert.AreEqual(20, seen.Stage);
            Assert.AreEqual(3, seen.Depth);
            Assert.AreEqual(target.Id, seen.PrimaryEntityId);
            Assert.AreEqual(5, (int)seen.InputParameters["Extra"]);
            Assert.AreEqual(pre.Id, seen.PreEntityImages["PreImage"].Id);
            Assert.AreEqual(post.Id, seen.PostEntityImages["PostImage"].Id);
            Assert.AreSame(ctx.Outputs, seen.OutputParameters);
        }

        private static Entity Account(string name, string city)
        {
            var e = new Entity("account");
            e["name"] = name;
            e["city"] = city;
            return e;
        }

        private static IEnumerable<Entity> Query(InMemoryOrganizationService svc, string attr, ConditionOperator op, params object[] values)
        {
            var q = new QueryExpression("account") { ColumnSet = new ColumnSet(true) };
            q.Criteria.AddCondition(attr, op, values);
            return svc.RetrieveMultiple(q).Entities;
        }
    }
}
