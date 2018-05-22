using GVFS.Common.Prefetch;
using GVFS.Tests.Should;
using GVFS.UnitTests.Mock.FileSystem;
using NUnit.Framework;

namespace GVFS.UnitTests.Prefetch
{
    [TestFixture]
    public class PrefetchHelperTests
    {
        [TestCase]
        public void AppendToNewlineSeparatedFileTests()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(@"mock:\GVFS\UnitTests\Repo", null, null));

            // Validate can write to a file that doesn't exist.
            const string TestFileName = @"mock:\GVFS\UnitTests\Repo\appendTest";
            PrefetchHelper.AppendToNewlineSeparatedFile(fileSystem, TestFileName, "expected content line 1");
            fileSystem.ReadAllText(TestFileName).ShouldEqual("expected content line 1\n");

            // Validate that if the file doesn't end in a newline it gets a newline added.
            fileSystem.WriteAllText(TestFileName, "existing content");
            PrefetchHelper.AppendToNewlineSeparatedFile(fileSystem, TestFileName, "expected line 2");
            fileSystem.ReadAllText(TestFileName).ShouldEqual("existing content\nexpected line 2\n");

            // Validate that if the file ends in a newline, we don't end up with two newlines
            fileSystem.WriteAllText(TestFileName, "existing content\n");
            PrefetchHelper.AppendToNewlineSeparatedFile(fileSystem, TestFileName, "expected line 2");
            fileSystem.ReadAllText(TestFileName).ShouldEqual("existing content\nexpected line 2\n");
        }
    }
}