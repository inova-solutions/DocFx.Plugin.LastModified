using System;
using System.Collections.Generic;
using System.IO;
using LibGit2Sharp;

namespace DocFx.Plugin.LastModified.Helpers
{
    /// <summary>
    ///     Provides methods for repository-related operations.
    /// </summary>
    public static class RepositoryHelper
    {
        /// <summary>
        ///     Returns the commit information for the specified file.
        /// </summary>
        /// <param name="repo">The repository to query against.</param>
        /// <param name="srcPath">The path of the file.</param>
        /// <returns>
        ///     A <see cref="Commit"/> object containing the information of the commit.
        /// </returns>
        //public static Commit GetCommitInfo(this Repository repo, string srcPath)
        //{
        //    if (repo == null) throw new ArgumentNullException(nameof(repo));
        //    if (srcPath == null) throw new ArgumentNullException(nameof(srcPath));

        //    // Hacky solution because libgit2sharp does not provide an easy way
        //    // to get the root dir of the repo
        //    // and for some reason does not work with forward-slash
        //    var repoRoot = repo.Info.Path.Replace('\\', '/').Replace(".git/", "");
        //    if (string.IsNullOrEmpty(repoRoot))
        //        throw new DirectoryNotFoundException("Cannot obtain the root directory of the repository.");
        //    Logger.LogVerbose($"Repository root: {repoRoot}");

        //    // Remove root dir from absolute path to transform into relative path
        //    var sourcePath = srcPath.Replace('\\', '/').Replace(repoRoot, "");
        //    Logger.LogVerbose($"Obtaining information from {sourcePath}, from repo {repo.Info.Path}...");

        //    // See libgit2sharp#1520 for sort issue
        //    var logEntry = repo.Commits
        //        .QueryBy(sourcePath, new CommitFilter { SortBy = CommitSortStrategies.Topological })
        //        .FirstOrDefault();
        //    Logger.LogVerbose($"Finished query for {sourcePath}.");
        //    return logEntry?.Commit;
        //}

        public static string GetRelativePath(this Repository repo, string srcPath)
        {
            if (repo == null) throw new ArgumentNullException(nameof(repo));
            if (srcPath == null) throw new ArgumentNullException(nameof(srcPath));

            // Hacky solution because libgit2sharp does not provide an easy way
            // to get the root dir of the repo
            // and for some reason does not work with forward-slash
            var repoRoot = repo.Info.Path.Replace('\\', '/').Replace(".git/", "");
            if (string.IsNullOrEmpty(repoRoot))
                throw new DirectoryNotFoundException("Cannot obtain the root directory of the repository.");

            // Remove root dir from absolute path to transform into relative path
            return srcPath.Replace('\\', '/').Replace(repoRoot, "");
        }

        public static Commit GetLastCommitForFile(this Repository repo, string path)
        {
            var commit = repo.Head.Tip;
            var gitObj = commit[path].Target;

            var set = new HashSet<string>();
            var queue = new Queue<Commit>();
            queue.Enqueue(commit);
            set.Add(commit.Sha);

            while (queue.Count > 0)
            {
                commit = queue.Dequeue();
                var go = false;
                foreach (var parent in commit.Parents)
                {
                    var tree = parent[path];
                    if (tree == null)
                        continue;
                    var eq = tree.Target.Sha == gitObj.Sha;
                    if (eq && set.Add(parent.Sha))
                        queue.Enqueue(parent);
                    go = go || eq;
                }
                if (!go)
                    break;
            }
            return commit;
        }
    }
}