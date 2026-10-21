using System;
using Microsoft.Xrm.Sdk;

namespace Dvx.Plugin.Sdk
{
    /// <summary>Base class for all plugins. Stateless: all per-call data lives in <see cref="PluginRuntime"/>.</summary>
    internal abstract class PluginBase : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException("serviceProvider");

            var runtime = new PluginRuntime(serviceProvider);
            var context = runtime.PluginContext;
            runtime.Trace(GetType().FullName + ": Execute " + context.MessageName + " "
                + context.PrimaryEntityName + " depth " + context.Depth);

            try
            {
                ExecuteCore(runtime);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                runtime.Trace(GetType().FullName + ": " + ex.GetType().Name + ": " + ex.Message);
                throw new InvalidPluginExecutionException(GetType().FullName + ": " + ex.Message, ex);
            }
        }

        protected abstract void ExecuteCore(PluginRuntime runtime);
    }
}
