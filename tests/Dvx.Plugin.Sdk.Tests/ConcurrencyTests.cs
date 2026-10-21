using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dvx.Plugin.Sdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace Dvx_Plugin_Sdk_Tests
{
    [TestClass]
    public class ConcurrencyTests
    {
        private sealed class EchoPlugin : PluginBase
        {
            protected override void ExecuteCore(PluginRuntime runtime)
            {
                runtime.Shared["id"] = runtime.Target.Id;
                Thread.Yield();
                runtime.PluginContext.OutputParameters["echo"] = runtime.Shared["id"];
            }
        }

        [TestMethod]
        public void SamePluginInstance_ParallelExecutions_DoNotShareState()
        {
            var plugin = new EchoPlugin();
            var runs = Enumerable.Range(0, 64).Select(i =>
            {
                var id = Guid.NewGuid();
                return new { Id = id, Services = FakeServices.Create(target: new Entity("account", id)) };
            }).ToList();

            Parallel.ForEach(runs, new ParallelOptions { MaxDegreeOfParallelism = 16 },
                r => plugin.Execute(r.Services.Provider));

            foreach (var r in runs)
            {
                Assert.AreEqual(r.Id, r.Services.Outputs["echo"]);
            }
        }

        private const int Parallelism = 64;

        // Runs one action per index on its own thread so that all executions really overlap
        // (the actions rendezvous on a Barrier). Rethrows the first failure.
        private static void RunOverlapped(Action<int> action)
        {
            var errors = new List<Exception>();
            var threads = Enumerable.Range(0, Parallelism).Select(i => new Thread(() =>
            {
                try { action(i); }
                catch (Exception ex) { lock (errors) errors.Add(ex); }
            })).ToList();
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();
            if (errors.Count > 0) throw new AssertFailedException("Parallel run failed: " + errors[0], errors[0]);
        }

        private static void Rendezvous(Barrier barrier)
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Executions did not overlap.");
        }

        private sealed class EchoFlow : FlowBase
        {
            private readonly Barrier _barrier;
            public EchoFlow(PluginRuntime runtime, Barrier barrier) : base(runtime) { _barrier = barrier; }
            public override void Execute()
            {
                var id = Runtime.Target.Id;
                Rendezvous(_barrier);
                Runtime.PluginContext.OutputParameters["echo"] = id;
            }
        }

        private sealed class EchoEntityPlugin : EntityPluginBase
        {
            private readonly Barrier _barrier;
            public EchoEntityPlugin(Barrier barrier) { _barrier = barrier; }
            protected override IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime)
            {
                yield return new EchoFlow(runtime, _barrier);
            }
        }

        [TestMethod]
        public void EntityPluginBase_SameInstance_64ParallelOverlappingExecutions_DoNotShareState()
        {
            using (var barrier = new Barrier(Parallelism))
            {
                var plugin = new EchoEntityPlugin(barrier);
                var ids = Enumerable.Range(0, Parallelism).Select(_ => Guid.NewGuid()).ToArray();
                var services = ids.Select(id => FakeServices.Create(target: new Entity("account", id))).ToArray();

                RunOverlapped(i => plugin.Execute(services[i].Provider));

                for (int i = 0; i < Parallelism; i++)
                {
                    Assert.AreEqual(ids[i], services[i].Outputs["echo"]);
                    // Each run's own tracing service received exactly its own flow traces.
                    Assert.AreEqual(1, services[i].Traces.Count(t => t == "EchoFlow: start"));
                    Assert.AreEqual(1, services[i].Traces.Count(t => t == "EchoFlow: end"));
                }
            }
        }

        private sealed class EchoRequest
        {
            [CustomApiRequestParameter("contoso_Value", CustomApiFieldType.Guid)]
            public Guid Value { get; set; }
        }

        private sealed class EchoResponse
        {
            [CustomApiResponseProperty("contoso_Echo", CustomApiFieldType.Guid)]
            public Guid Echo { get; set; }
        }

        private sealed class EchoApi : CustomApiBase<EchoRequest, EchoResponse>
        {
            private readonly Barrier _barrier;
            public EchoApi(Barrier barrier) { _barrier = barrier; }
            protected override EchoResponse Handle(EchoRequest request, PluginRuntime runtime)
            {
                var value = request.Value;
                Rendezvous(_barrier);
                return new EchoResponse { Echo = value };
            }
        }

        [TestMethod]
        public void CustomApiBase_SameInstance_64ParallelOverlappingExecutions_DoNotShareState()
        {
            using (var barrier = new Barrier(Parallelism))
            {
                var api = new EchoApi(barrier);
                var ids = Enumerable.Range(0, Parallelism).Select(_ => Guid.NewGuid()).ToArray();
                var services = ids.Select(id =>
                {
                    var s = FakeServices.Create();
                    s.Inputs["contoso_Value"] = id;
                    return s;
                }).ToArray();

                RunOverlapped(i => api.Execute(services[i].Provider));

                for (int i = 0; i < Parallelism; i++)
                {
                    Assert.AreEqual(ids[i], services[i].Outputs["contoso_Echo"]);
                }
            }
        }
    }
}
