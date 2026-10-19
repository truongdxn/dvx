using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx_Cli_Tests
{
    [TestClass]
    public class ScaffoldTests
    {
        [TestMethod]
        public void Scaffold_Builds()
        {
            Assert.IsNotNull(typeof(ScaffoldTests).Assembly);
        }
    }
}
