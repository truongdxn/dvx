using FakeItEasy;
using Microsoft.Xrm.Sdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.Plugins.Tests
{
    [TestClass]
    public class AccountPluginTests
    {
        [TestMethod]
        public void Sees_internal_type_and_fakeiteasy()
        {
            var plugin = new AccountPlugin(); // internal type of Contoso.Plugins
            var service = A.Fake<IOrganizationService>(); // FakeItEasy injected by Dvx.targets
            Assert.AreEqual("hello", plugin.Greeting);
            Assert.IsNotNull(service);
        }
    }

    // Matches the spike rule so the test project shows the diagnostic too.
    public class HelperPlugin { }
}
