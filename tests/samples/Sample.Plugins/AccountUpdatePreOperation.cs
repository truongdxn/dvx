using System.Collections.Generic;
using Dvx.Plugin.Sdk;

namespace Contoso.Plugins
{
    [PluginStep("Update", Account.EntityLogicalName, Stage.PreOperation, Mode.Synchronous,
        FilteringAttributes = new[] { Account.Fields.Name, Account.Fields.CreditLimit },
        PreImageAttributes = new[] { Account.Fields.Name, Account.Fields.CreditLimit },
        Rank = 1,
        Description = "Validate account changes")]
    internal sealed class AccountUpdatePreOperation : EntityPluginBase
    {
        protected override IEnumerable<FlowBase> CreateFlows(PluginRuntime runtime)
        {
            yield return new ValidateAccountFlow(runtime);
        }

        private sealed class ValidateAccountFlow : FlowBase
        {
            public ValidateAccountFlow(PluginRuntime runtime) : base(runtime) { }

            public override void Execute() { }
        }
    }
}
