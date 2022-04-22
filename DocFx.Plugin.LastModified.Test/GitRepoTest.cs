using DocFx.Plugin.LastModified.Helpers;
using LibGit2Sharp;
using NUnit.Framework;

namespace DocFx.Plugin.LastModified.Test
{
    [TestFixture]
    public class GitRepoTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void DiscoveryWorks()
        {
            var path = "C:/repos/inova/doc/intern/_work";
            var file = @"C:/repos/inova/doc/intern/product/score/Specification/Snit/ExcelIn/sgov_sd_excelIn_features.md";
            var gitDirectory = Repository.Discover(path);
            Assert.That(gitDirectory, Is.Not.Null);

            var repo = new Repository(gitDirectory);
            Assert.That(repo, Is.Not.Null);
            var sourcePath = repo.GetRelativePath(file);
            var commit = repo.GetLastCommitForFile(sourcePath);
            Assert.That(commit, Is.Not.Null);
        }
    }
}