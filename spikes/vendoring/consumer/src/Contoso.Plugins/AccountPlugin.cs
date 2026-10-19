using System;
using Microsoft.Xrm.Sdk;

namespace Contoso.Plugins
{
    // Internal on purpose: the test project can only see it through InternalsVisibleTo.
    internal class AccountPlugin : IPlugin
    {
        internal string Greeting => "hello";

        public void Execute(IServiceProvider serviceProvider) { }
    }
}
