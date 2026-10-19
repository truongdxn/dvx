using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx_Integration_Tests
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
