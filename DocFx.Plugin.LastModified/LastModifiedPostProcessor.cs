using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DocFx.Plugin.LastModified.Helpers;
using HtmlAgilityPack;
using LibGit2Sharp;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

namespace DocFx.Plugin.LastModified
{
    /// <summary>
    ///     Post-processor responsible for injecting last modified date according to commit or file modified date.
    /// </summary>
    [Export(nameof(LastModifiedPostProcessor), typeof(IPostProcessor))]
    public class LastModifiedPostProcessor : IPostProcessor
    {
        private int _addedFiles;
        private Repository _repo;
        private static CultureInfo _localCulture = new CultureInfo("de-CH");

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
            => metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            var versionInfo = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion ??
                              Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.LogInfo($"Version: {versionInfo}");
            Logger.LogInfo("Begin adding last modified date to items...");

            // attempt to fetch git repo from the current project
            Logger.LogInfo($"Looking for git repo {manifest.SourceBasePath}");
            var gitDirectory = Repository.Discover(manifest.SourceBasePath);
            if (gitDirectory != null)
            {
                Logger.LogInfo($"using git directory {gitDirectory}");
                _repo = new Repository(gitDirectory);
                Logger.LogInfo($"connected to git repo: {((_repo == null) ? false : true)}");
            }

            foreach (var manifestItem in manifest.Files.Where(x => x.DocumentType == "Conceptual"))
                foreach (var manifestItemOutputFile in manifestItem.OutputFiles)
                {
                    var sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                    if (sourcePath.Contains("_work\\"))
                    {
                        sourcePath = sourcePath.Replace("_work\\", "");
                    }
                    var outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                    if (_repo != null)
                    {
                        Logger.LogInfo($"checking source path: {sourcePath} for output {outputPath}");
                        string repoPath = null;
                        Commit commitInfo = null;
                        try
                        {
                            repoPath = _repo.GetRelativePath(sourcePath);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to get repo path for file {sourcePath} ({ex.Message})");
                        }
                        if (repoPath != null)
                        {
                            commitInfo = _repo.GetLastCommitForFile(repoPath);
                        }
                        if (commitInfo != null)
                        {
                            Logger.LogVerbose("Assigning commit date...");
                            var lastModified = commitInfo.Author.When;

                            //var commitHeaderBuilder = new StringBuilder();
                            //Logger.LogVerbose("Appending commit author and email...");
                            //commitHeaderBuilder.AppendLine($"Author:    {commitInfo.Author.Name}");
                            //Logger.LogVerbose("Appending commit SHA...");
                            //commitHeaderBuilder.AppendLine($"Commit:    {commitInfo.Sha}");

                            //var commitHeader = commitHeaderBuilder.ToString();
                            //// truncate to 200 in case of huge commit body
                            //var commitBody = commitInfo.Message.Truncate(300);
                            //Logger.LogVerbose($"Writing {lastModified} with reason for {outputPath}...");
                            //WriteModifiedDate(outputPath, lastModified, commitHeader, commitBody);
                            WriteModifiedDate(outputPath, lastModified, commit: commitInfo);
                            continue;
                        }
                    }

                    var fileLastModified = File.GetLastWriteTimeUtc(sourcePath);
                    Logger.LogVerbose($"Writing {fileLastModified} for {outputPath}...");
                    WriteModifiedDate(outputPath, fileLastModified);
                }

            // dispose repo after usage
            _repo?.Dispose();

            Logger.LogInfo($"Added modification date to {_addedFiles} conceptual articles.");
            return manifest;
        }

        private void WriteModifiedDate(string outputPath, DateTimeOffset modifiedDate, string commitHeader = null, string commitBody = null, Commit commit = null)
        {
            if (outputPath == null)
                return;

            // load the document
            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);

            // check for article container
            var articleNode = htmlDoc.DocumentNode.SelectSingleNode("//article[contains(@class, 'content wrap')]");
            if (articleNode == null)
            {
                Logger.LogDiagnostic("ArticleNode not found, returning.");
                return;
            }


            var modifiedString = $"Bearbeitet am {modifiedDate.ToLocalTime().ToString("dd.MM.yyyy")}";
            if (commit != null)
            {
                var author = commit.Author.Name;
                if (author != null)
                {
                    modifiedString = $"{modifiedString}<br>von {author}";
                }
            }

            var modParent = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'contribution')]/ul");
            if (modParent != null)
            {
                AppendModifiedListElement(htmlDoc, modParent, modifiedString);
            }

            //var paragraphNode = htmlDoc.CreateElement("p");
            //AppendModifiedSpan(htmlDoc, paragraphNode, modifiedString);
            //paragraphNode.InnerHtml = modifiedString;
            //var separatorNode = htmlDoc.CreateElement("hr");
            //articleNode.AppendChild(separatorNode);
            //articleNode.AppendChild(paragraphNode);

            //if (!string.IsNullOrEmpty(commitHeader))
            //{
            //    // inject collapsible container script
            //    InjectCollapseScript(htmlDoc);

            //    // create collapse container
            //    var collapsibleNode = htmlDoc.CreateElement("div");
            //    collapsibleNode.SetAttributeValue("class", "collapse-container last-modified");
            //    collapsibleNode.SetAttributeValue("id", "accordion");
            //    var reasonHeaderNode = htmlDoc.CreateElement("span");
            //    reasonHeaderNode.InnerHtml = "<span class=\"arrow-r\"></span>Commit Message";
            //    var reasonContainerNode = htmlDoc.CreateElement("div");

            //    // inject header
            //    var preCodeBlockNode = htmlDoc.CreateElement("pre");
            //    var codeBlockNode = htmlDoc.CreateElement("code");
            //    codeBlockNode.InnerHtml = commitHeader;
            //    preCodeBlockNode.AppendChild(codeBlockNode);
            //    reasonContainerNode.AppendChild(preCodeBlockNode);

            //    // inject body
            //    preCodeBlockNode = htmlDoc.CreateElement("pre");
            //    codeBlockNode = htmlDoc.CreateElement("code");
            //    codeBlockNode.SetAttributeValue("class", "xml");
            //    codeBlockNode.InnerHtml = commitBody;
            //    preCodeBlockNode.AppendChild(codeBlockNode);
            //    reasonContainerNode.AppendChild(preCodeBlockNode);

            //    // inject the entire block
            //    collapsibleNode.AppendChild(reasonHeaderNode);
            //    collapsibleNode.AppendChild(reasonContainerNode);
            //    articleNode.AppendChild(collapsibleNode);
            //}

            htmlDoc.Save(outputPath);
            _addedFiles++;
        }

        //private void AppendModifiedSpan(HtmlDocument htmlDoc, HtmlNode parentNode, string modifiedString)
        //{
        //    var spanNode = htmlDoc.CreateElement("span");
        //    spanNode.SetAttributeValue("class", "");
        //    spanNode.InnerHtml = modifiedString;
        //    parentNode.AppendChild(spanNode);
        //}

        private void AppendModifiedListElement(HtmlDocument htmlDoc, HtmlNode parentNode, string modifiedString)
        {
            var node = htmlDoc.CreateElement("li");
            node.SetAttributeValue("class", "contribution-mod");
            node.InnerHtml = modifiedString;
            //parentNode.AppendChild(node);
            parentNode.InsertBefore(node, parentNode.FirstChild);
        }

        /// <summary>
        ///     Injects script required for collapsible dropdown menu.
        /// </summary>
        /// <seealso cref="!:https://github.com/jordnkr/collapsible" />
        private static void InjectCollapseScript(HtmlDocument htmlDoc)
        {
            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

            var accordionNode = htmlDoc.CreateElement("script");
            accordionNode.InnerHtml = @"
  $( function() {
    $( ""#accordion"" ).collapsible();
  } );";
            bodyNode.AppendChild(accordionNode);

            var collapsibleScriptNode = htmlDoc.CreateElement("script");
            collapsibleScriptNode.SetAttributeValue("type", "text/javascript");
            collapsibleScriptNode.SetAttributeValue("src",
                "https://cdn.rawgit.com/jordnkr/collapsible/master/jquery.collapsible.min.js");
            bodyNode.AppendChild(collapsibleScriptNode);

            var headNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
            var collapsibleCssNode = htmlDoc.CreateElement("link");
            collapsibleCssNode.SetAttributeValue("rel", "stylesheet");
            collapsibleCssNode.SetAttributeValue("href",
                "https://cdn.rawgit.com/jordnkr/collapsible/master/collapsible.css");
            headNode.AppendChild(collapsibleCssNode);
        }
    }
}