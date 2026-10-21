using System;

namespace Dvx.Plugin.Sdk
{
    /// <summary>One unit of business logic, run by an <see cref="EntityPluginBase"/>.</summary>
    internal abstract class FlowBase
    {
        protected FlowBase(PluginRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            Runtime = runtime;
        }

        protected PluginRuntime Runtime { get; private set; }

        /// <summary>Business conditions only.</summary>
        public virtual bool CanExecute()
        {
            return true;
        }

        public abstract void Execute();
    }
}
