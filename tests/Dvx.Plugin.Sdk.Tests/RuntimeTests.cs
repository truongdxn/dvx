using System;
using System.Collections.Generic;
using Dvx.Plugin.Sdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace Dvx_Plugin_Sdk_Tests
{
    [TestClass]
    public class RuntimeTests
    {
        private abstract class FlowStub : FlowBase
        {
            private readonly bool _can;
            private readonly List<string> _log;
            protected FlowStub(PluginRuntime r, bool can, List<string> log) : base(r) { _can = can; _log = log; }
            public override bool CanExecute() { return _can; }
            public override void Execute() { _log.Add(GetType().Name); }
        }

        private sealed class A : FlowStub { public A(PluginRuntime r, bool c, List<string> l) : base(r, c, l) { } }
        private sealed class B : FlowStub { public B(PluginRuntime r, bool c, List<string> l) : base(r, c, l) { } }

        private sealed class TestEntityPlugin : EntityPluginBase
        {
            private readonly List<string> _log;
            private readonly int? _maxDepth;
            public TestEntityPlugin(List<string> log, int? maxDepth = null) { _log = log; _maxDepth = maxDepth; }
            protected override int? MaxDepth { get { return _maxDepth; } }
            protected override IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime)
            {
                yield return new A(runtime, true, _log);
                yield return new B(runtime, false, _log);
            }
        }

        private sealed class ThrowingPlugin : PluginBase
        {
            private readonly Exception _ex;
            public ThrowingPlugin(Exception ex) { _ex = ex; }
            protected override void ExecuteCore(PluginRuntime runtime) { throw _ex; }
        }

        private sealed class ImagePlugin : PluginBase
        {
            public Entity Pre;
            public Entity Post;
            protected override void ExecuteCore(PluginRuntime runtime) { Pre = runtime.PreImage; Post = runtime.PostImage; }
        }

        private sealed class Req
        {
            [CustomApiRequestParameter("contoso_Amount", CustomApiFieldType.Money)]
            public Money Amount { get; set; }
            [CustomApiRequestParameter("contoso_Note", CustomApiFieldType.String, IsOptional = true)]
            public string Note { get; set; } = "default";
        }

        private sealed class Resp
        {
            [CustomApiResponseProperty("contoso_Discount", CustomApiFieldType.Decimal)]
            public decimal Discount { get; set; }
        }

        private sealed class DiscountApi : CustomApiBase<Req, Resp>
        {
            public Req Seen;
            protected override Resp Handle(Req request, PluginRuntime runtime)
            {
                Seen = request;
                return new Resp { Discount = 10m };
            }
        }

        [TestMethod]
        public void EntityPlugin_RunsOnlyFlowsWhoseCanExecuteIsTrue()
        {
            var log = new List<string>();
            var s = FakeServices.Create();
            new TestEntityPlugin(log).Execute(s.Provider);
            CollectionAssert.AreEqual(new[] { "A" }, log);
        }

        [TestMethod]
        public void EntityPlugin_TracesFlowNameBeforeAndAfter()
        {
            var s = FakeServices.Create();
            new TestEntityPlugin(new List<string>()).Execute(s.Provider);
            int start = s.Traces.IndexOf("A: start");
            int end = s.Traces.IndexOf("A: end");
            Assert.IsTrue(start >= 0, "missing start trace");
            Assert.IsTrue(end > start, "end trace must follow start trace");
        }

        [TestMethod]
        public void EntityPlugin_MaxDepthNull_RunsAtDepth5()
        {
            var log = new List<string>();
            var s = FakeServices.Create(depth: 5);
            new TestEntityPlugin(log, null).Execute(s.Provider);
            CollectionAssert.AreEqual(new[] { "A" }, log);
        }

        [TestMethod]
        public void EntityPlugin_MaxDepth1_SkipsAtDepth2()
        {
            var log = new List<string>();
            var s = FakeServices.Create(depth: 2);
            new TestEntityPlugin(log, 1).Execute(s.Provider);
            Assert.AreEqual(0, log.Count);
        }

        [TestMethod]
        public void PluginBase_RethrowsInvalidPluginExecutionExceptionUnchanged()
        {
            var original = new InvalidPluginExecutionException("business rule");
            var s = FakeServices.Create();
            var thrown = Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => new ThrowingPlugin(original).Execute(s.Provider));
            Assert.AreSame(original, thrown);
        }

        [TestMethod]
        public void PluginBase_WrapsOtherExceptions_InInvalidPluginExecutionException()
        {
            var inner = new InvalidOperationException("boom");
            var s = FakeServices.Create();
            var thrown = Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => new ThrowingPlugin(inner).Execute(s.Provider));
            Assert.AreSame(inner, thrown.InnerException);
            StringAssert.Contains(thrown.Message, "boom");
            StringAssert.Contains(thrown.Message, typeof(ThrowingPlugin).FullName);
        }

        [TestMethod]
        public void CustomApi_MapsInputsAndOutputs()
        {
            var s = FakeServices.Create();
            s.Inputs["contoso_Amount"] = new Money(100m);
            var api = new DiscountApi();
            api.Execute(s.Provider);
            Assert.AreEqual(100m, api.Seen.Amount.Value);
            Assert.AreEqual(10m, s.Outputs["contoso_Discount"]);
        }

        [TestMethod]
        public void CustomApi_MissingOptionalParameter_LeavesDefault()
        {
            var s = FakeServices.Create();
            s.Inputs["contoso_Amount"] = new Money(1m);
            var api = new DiscountApi();
            api.Execute(s.Provider);
            Assert.AreEqual("default", api.Seen.Note);
        }

        [TestMethod]
        public void CustomApi_NonAssignableInput_ThrowsNamingParameterWithoutValue()
        {
            var s = FakeServices.Create();
            s.Inputs["contoso_Amount"] = "secret-value-123";
            var ex = Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => new DiscountApi().Execute(s.Provider));
            StringAssert.Contains(ex.Message, "contoso_Amount");
            Assert.IsFalse(ex.Message.Contains("secret-value-123"));
        }

        [TestMethod]
        public void Runtime_ImagesReadByFixedAliases()
        {
            var s = FakeServices.Create();
            var pre = new Entity("account", Guid.NewGuid());
            var post = new Entity("account", Guid.NewGuid());
            s.PreImages["PreImage"] = pre;
            s.PostImages["PostImage"] = post;
            var plugin = new ImagePlugin();
            plugin.Execute(s.Provider);
            Assert.AreSame(pre, plugin.Pre);
            Assert.AreSame(post, plugin.Post);
        }
    }
}
