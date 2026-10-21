using System.Collections.Generic;

namespace Dvx.Plugin.Sdk
{
    /// <summary>Plugin that runs an explicit, ordered list of flows.</summary>
    internal abstract class EntityPluginBase : PluginBase
    {
        /// <summary>Maximum allowed call depth; null disables the guard.</summary>
        protected virtual int? MaxDepth
        {
            get { return null; }
        }

        protected abstract IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime);

        protected override void ExecuteCore(PluginRuntime runtime)
        {
            var max = MaxDepth;
            if (max.HasValue && runtime.PluginContext.Depth > max.Value)
            {
                runtime.Trace(GetType().Name + ": skipped at depth " + runtime.PluginContext.Depth);
                return;
            }

            var flows = CreateFlows(runtime);
            if (flows == null) return;

            foreach (var flow in flows)
            {
                var name = flow.GetType().Name;
                if (!flow.CanExecute())
                {
                    runtime.Trace(name + ": skipped");
                    continue;
                }

                runtime.Trace(name + ": start");
                flow.Execute();
                runtime.Trace(name + ": end");
            }
        }
    }
}
