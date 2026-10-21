using System.Collections.Generic;
using Dvx.Plugin.Sdk;

namespace Fabrikam.Invalid
{
    // Negative samples for AttributeReader: Dataverse cannot instantiate these types.
    [PluginStep("Update", "account", Stage.PreOperation, Mode.Synchronous)]
    internal abstract class AbstractStepPlugin : EntityPluginBase
    {
    }

    [PluginStep("Create", "account", Stage.PostOperation, Mode.Synchronous)]
    internal sealed class GenericStepPlugin<T> : EntityPluginBase
    {
        protected override IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime)
        {
            yield break;
        }
    }
}
