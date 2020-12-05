using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ResourcesRepository.Tests
{
    [TestClass]
    public class RepoTests
    {
        [TestMethod]
        public void TestMethod1()
        {
        }

        [TestMethod]
        public void TestMethod2() 
        {
            throw new System.Exception("Haha");
        }
    }
}