using System.Collections.Generic;
using Dvx.Plugin.Sdk;

namespace Contoso.Plugins
{
    // First step has no named arguments: pins reader defaults (absent arrays stay null, Rank is 1).
    // Second step has an explicit null array and an explicit empty array.
    [PluginStep("Create", "contact", Stage.PostOperation, Mode.Asynchronous)]
    [PluginStep("Delete", "contact", Stage.PreValidation, Mode.Synchronous,
        PreImageAttributes = null, PostImageAttributes = new string[0], Rank = 7)]
    internal sealed class ContactCreatePostOperation : EntityPluginBase
    {
        protected override IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime)
        {
            yield break;
        }
    }
}
