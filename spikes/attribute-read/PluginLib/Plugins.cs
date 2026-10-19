using System;
using Dvx.Plugin.Sdk;
using Microsoft.Xrm.Sdk;

namespace SpikePlugins
{
    internal static class Account
    {
        public const string EntityLogicalName = "account";

        internal static class Fields
        {
            public const string Name = "name";
            public const string CreditLimit = "creditlimit";
        }
    }

    // string[] named args built from const fields; PreImageAttributes absent; PostImageAttributes empty.
    [PluginStep("Update", Account.EntityLogicalName, Stage.PreOperation, Mode.Synchronous,
        FilteringAttributes = new[] { Account.Fields.Name, Account.Fields.CreditLimit },
        PostImageAttributes = new string[0],
        Description = "Validates credit limit")]
    internal sealed class AccountUpdatePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider) { }
    }

    // Two steps on one class; explicit null; Rank set; enum ctor args differ.
    [PluginStep("Create", Account.EntityLogicalName, Stage.PostOperation, Mode.Asynchronous,
        PreImageAttributes = null, Rank = 5)]
    [PluginStep("Delete", Account.EntityLogicalName, Stage.PreValidation, Mode.Synchronous,
        PreImageAttributes = new[] { Account.Fields.Name })]
    internal sealed class AccountMultiPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider) { }
    }

    // No named args at all.
    [PluginStep("Assign", Account.EntityLogicalName, Stage.PostOperation, Mode.Synchronous)]
    internal sealed class AccountPlainPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider) { }
    }
}
