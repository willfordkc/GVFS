using GVFS.Common;
using GVFS.Windows;
using NUnit.Framework;

namespace GVFS.UnitTests
{
    [SetUpFixture]
    public class Setup
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            GVFSPlatform.Register(new WindowsPlatform());
        }
    }
}
