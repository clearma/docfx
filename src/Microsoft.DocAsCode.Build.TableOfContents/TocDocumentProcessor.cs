﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.TableOfContents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.IO;

    using Microsoft.DocAsCode.Build.Common;
    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.DataContracts.Common;
    using Microsoft.DocAsCode.Plugins;
    using Microsoft.DocAsCode.Utility;

    [Export(typeof(IDocumentProcessor))]
    public class TocDocumentProcessor : DisposableDocumentProcessor
    {
        public override string Name => nameof(TocDocumentProcessor);

        [ImportMany(nameof(TocDocumentProcessor))]
        public override IEnumerable<IDocumentBuildStep> BuildSteps { get; set; }

        public override ProcessingPriority GetProcessingPriority(FileAndType file)
        {
            if (file.Type == DocumentType.Article && Utility.IsSupportedFile(file.File))
            {
                return ProcessingPriority.High;
            }

            return ProcessingPriority.NotSupported;
        }

        public override FileModel Load(FileAndType file, ImmutableDictionary<string, object> metadata)
        {
            var filePath = file.FullPath;
            var tocViewModel = Utility.LoadSingleToc(filePath);
            var toc = new TocItemViewModel
            {
                Items = tocViewModel
            };

            var repoDetail = GitUtility.GetGitDetail(filePath);
            var displayLocalPath = repoDetail?.RelativePath ?? filePath;

            // todo : metadata.
            return new FileModel(file, toc)
            {
                Uids = new[] { new UidDefinition(file.File, displayLocalPath) }.ToImmutableArray(),
                LocalPathFromRepoRoot = displayLocalPath,
                Properties =
                        {
                            LinkToFiles = new HashSet<string>(),
                            LinkToUids = new HashSet<string>(),
                        },
            };
        }

        public override SaveResult Save(FileModel model)
        {
            return new SaveResult
            {
                DocumentType = "Toc",
                FileWithoutExtension = Path.ChangeExtension(model.File, null),
            };
        }

        public override void UpdateHref(FileModel model, IDocumentBuildContext context)
        {
            var toc = (TocItemViewModel)model.Content;
            var key = model.Key;

            // Add current folder to the toc mapping, e.g. `a/` maps to `a/toc`
            var directory = ((RelativePath)key).GetPathFromWorkingFolder().GetDirectoryPath();
            context.RegisterToc(key, directory);
            UpdateTocItemHref(toc, model, context);
            var tocInfo = new TocInfo(key);
            if (toc.Homepage != null)
            {
                if (PathUtility.IsRelativePath(toc.Homepage))
                {
                    var pathToRoot = ((RelativePath)model.File + (RelativePath)toc.Homepage).GetPathFromWorkingFolder();
                    tocInfo.Homepage = pathToRoot;
                }
            }

            context.RegisterTocInfo(tocInfo);
        }

        private void UpdateTocItemHref(TocItemViewModel toc, FileModel model, IDocumentBuildContext context)
        {
            ResolveUid(toc, model, context);

            // Have to register TocMap after uid is resolved
            RegisterTocMap(toc, model.Key, context);

            toc.Homepage = ResolveHref(toc.Homepage, model, context);
            toc.Href = ResolveHref(toc.Href, model, context);
            toc.TocHref = ResolveHref(toc.TocHref, model, context);
            if (toc.Items != null && toc.Items.Count > 0)
            {
                foreach (var item in toc.Items)
                {
                    UpdateTocItemHref(item, model, context);
                }
            }
        }

        private void ResolveUid(TocItemViewModel item, FileModel model, IDocumentBuildContext context)
        {
            if (item.Uid != null)
            {
                var xref = GetXrefFromUid(item.Uid, model, context);
                if (xref != null)
                {
                    item.Href = xref.Href;
                    if (string.IsNullOrEmpty(item.Name))
                    {
                        item.Name = xref.Name;
                    }

                    string nameForCSharp;
                    if (string.IsNullOrEmpty(item.NameForCSharp) && xref.TryGetValue("name.csharp", out nameForCSharp))
                    {
                        item.NameForCSharp = nameForCSharp;
                    }
                    string nameForVB;
                    if (string.IsNullOrEmpty(item.NameForVB) && xref.TryGetValue("name.vb", out nameForVB))
                    {
                        item.NameForVB = nameForVB;
                    }
                }
            }

            if (item.HomepageUid != null)
            {
                item.Homepage = GetXrefFromUid(item.HomepageUid, model, context)?.Href;
            }
        }

        private XRefSpec GetXrefFromUid(string uid, FileModel model, IDocumentBuildContext context)
        {
            var xref = context.GetXrefSpec(uid);
            if (xref == null)
            {
                Logger.LogWarning($"Unable to find file with uid \"{uid}\" referenced by TOC file \"{model.LocalPathFromRepoRoot}\"");
            }
            return xref;
        }

        private void RegisterTocMap(TocItemViewModel item, string key, IDocumentBuildContext context)
        {
            // If tocHref is set, href is originally RelativeFolder type, and href is set to the homepage of TocHref,
            // So in this case, TocHref should be used to in TocMap
            // TODO: what if user wants to set TocHref?
            var tocHref = item.TocHref;
            var tocHrefType = Utility.GetHrefType(tocHref);
            if (tocHrefType == HrefType.MarkdownTocFile || tocHrefType == HrefType.YamlTocFile)
            {
                context.RegisterToc(key, tocHref);
            }
            else
            {
                var href = item.Href; // Should be original href from working folder starting with ~
                if (Utility.IsSupportedRelativeHref(href))
                {
                    context.RegisterToc(key, href);
                }
            }
        }

        private string ResolveHref(string originalPathToFile, FileModel model, IDocumentBuildContext context)
        {
            if (!Utility.IsSupportedRelativeHref(originalPathToFile)) return originalPathToFile;

            string href = context.GetFilePath(originalPathToFile);

            if (href == null)
            {
                Logger.LogWarning($"Unable to find file \"{originalPathToFile}\" referenced by TOC file \"{model.LocalPathFromRepoRoot}\"");
                return originalPathToFile;
            }

            var relativePath = GetRelativePath(href, model.File);
            return ((RelativePath)relativePath).UrlEncode();
        }

        private string GetRelativePath(string pathFromWorkingFolder, string relativeToPath)
        {
            return ((RelativePath)pathFromWorkingFolder).MakeRelativeTo(((RelativePath)relativeToPath).GetPathFromWorkingFolder());
        }
    }
}
