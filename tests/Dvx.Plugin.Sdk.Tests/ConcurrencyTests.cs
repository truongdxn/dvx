using System;
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
    }
}
