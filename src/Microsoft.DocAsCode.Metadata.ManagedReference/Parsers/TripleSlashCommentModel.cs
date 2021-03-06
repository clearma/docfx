// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.XPath;

    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.DataContracts.ManagedReference;
    using Microsoft.DocAsCode.Utility;

    public class TripleSlashCommentModel
    {
        private const string idSelector = @"((?![0-9])[\w_])+[\w\(\)\.\{\}\[\]\|\*\^~#@!`,_<>:]*";
        private static Regex CommentIdRegex = new Regex(@"^(?<type>N|T|M|P|F|E):(?<id>" + idSelector + ")$", RegexOptions.Compiled);
        private static Regex LineBreakRegex = new Regex(@"\r?\n", RegexOptions.Compiled);
        private static Regex CodeElementRegex = new Regex(@"<code[^>]*>([\s\S]*?)</code>", RegexOptions.Compiled);

        private readonly ITripleSlashCommentParserContext _context;
        private readonly TripleSlashCommentTransformer _transformer = new TripleSlashCommentTransformer();

        public string Summary { get; private set; }
        public string Remarks { get; private set; }
        public string Returns { get; private set; }
        public List<CrefInfo> Exceptions { get; private set; }
        public List<CrefInfo> Sees { get; private set; }
        public List<CrefInfo> SeeAlsos { get; private set; }
        public List<string> Examples { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }
        public Dictionary<string, string> TypeParameters { get; private set; }

        private TripleSlashCommentModel(string xml, SyntaxLanguage language, ITripleSlashCommentParserContext context)
        {
            // Transform triple slash comment
            XDocument doc = _transformer.Transform(xml, language);

            _context = context;
            if (!context.PreserveRawInlineComments)
            {
                ResolveSeeCref(doc, context.AddReferenceDelegate);
                ResolveSeeAlsoCref(doc, context.AddReferenceDelegate);
            }
            var nav = doc.CreateNavigator();
            Summary = GetSummary(nav, context);
            Remarks = GetRemarks(nav, context);
            Returns = GetReturns(nav, context);

            Exceptions = GetExceptions(nav, context);
            Sees = GetSees(nav, context);
            SeeAlsos = GetSeeAlsos(nav, context);
            Examples = GetExamples(nav, context);
            Parameters = GetParameters(nav, context);
            TypeParameters = GetTypeParameters(nav, context);
        }

        public static TripleSlashCommentModel CreateModel(string xml, SyntaxLanguage language, ITripleSlashCommentParserContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrEmpty(xml)) return null;
            // Quick turnaround for badly formed XML comment
            if (xml.StartsWith("<!-- Badly formed XML comment ignored for member "))
            {
                Logger.LogWarning($"Invalid triple slash comment is ignored: {xml}");
                return null;
            }
            try
            {
                var model = new TripleSlashCommentModel(xml, language, context);
                return model;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        public string GetParameter(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return GetValue(name, Parameters);
        }

        public string GetTypeParameter(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return GetValue(name, TypeParameters);
        }

        private static string GetValue(string name, Dictionary<string, string> dictionary)
        {
            if (dictionary == null) return null;
            string description;
            if (dictionary.TryGetValue(name, out description))
            {
                return description;
            }
            return null;
        }

        /// <summary>
        /// Get summary node out from triple slash comments
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="normalize"></param>
        /// <returns></returns>
        /// <example>
        /// <code> <see cref="Hello"/></code>
        /// </example>
        private string GetSummary(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            // Resolve <see cref> to @ syntax
            // Also support <seealso cref>
            string selector = "/member/summary";
            return GetSingleNodeValue(nav, selector);
        }

        /// <summary>
        /// Get remarks node out from triple slash comments
        /// </summary>
        /// <remarks>
        /// <para>This is a sample of exception node</para>
        /// </remarks>
        /// <param name="xml"></param>
        /// <param name="normalize"></param>
        /// <returns></returns>
        private string GetRemarks(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            string selector = "/member/remarks";
            return GetSingleNodeValue(nav, selector);
        }

        private string GetReturns(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            // Resolve <see cref> to @ syntax
            // Also support <seealso cref>
            string selector = "/member/returns";
            return GetSingleNodeValue(nav, selector);
        }

        /// <summary>
        /// Get exceptions nodes out from triple slash comments
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="normalize"></param>
        /// <returns></returns>
        /// <exception cref="XmlException">This is a sample of exception node</exception>
        private List<CrefInfo> GetExceptions(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            string selector = "/member/exception";
            var result = GetMulitpleCrefInfo(nav, selector).ToList();
            if (result.Count == 0) return null;
            return result;
        }

        /// <summary>
        /// To get `see` tags out
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <see cref="SpecIdHelper"/>
        /// <see cref="SourceSwitch"/>
        private List<CrefInfo> GetSees(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            var result = GetMulitpleCrefInfo(nav, "/member/see").ToList();
            if (result.Count == 0) return null;
            return result;
        }

        /// <summary>
        /// To get `seealso` tags out
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <seealso cref="WaitForChangedResult"/>
        /// <seealso cref="http://google.com">ABCS</seealso>
        private List<CrefInfo> GetSeeAlsos(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            var result = GetMulitpleCrefInfo(nav, "/member/seealso").ToList();
            if (result.Count == 0) return null;
            return result;
        }

        /// <summary>
        /// To get `example` tags out
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <example> 
        /// This sample shows how to call the <see cref="GetExceptions(string, ITripleSlashCommentParserContext)"/> method.
        /// <code>
        /// class TestClass  
        /// { 
        ///     static int Main()  
        ///     { 
        ///         return GetExceptions(null, null).Count(); 
        ///     } 
        /// } 
        /// </code> 
        /// </example>
        private List<string> GetExamples(XPathNavigator nav, ITripleSlashCommentParserContext context)
        {
            // Resolve <see cref> to @ syntax
            // Also support <seealso cref>
            return GetMultipleExampleNodes(nav, "/member/example").ToList();
        }

        private Dictionary<string, string> GetListContent(XPathNavigator navigator, string xpath, string contentType, ITripleSlashCommentParserContext context)
        {
            var iterator = navigator.Select(xpath);
            var result = new Dictionary<string, string>();
            if (iterator == null) return result;
            foreach (XPathNavigator nav in iterator)
            {
                string name = nav.GetAttribute("name", string.Empty);
                string description = GetXmlValue(nav);
                if (!string.IsNullOrEmpty(name))
                {
                    if (result.ContainsKey(name))
                    {
                        string path = context.Source.Remote != null ? Path.Combine(context.Source.Remote.LocalWorkingDirectory, context.Source.Remote.RelativePath) : context.Source.Path;
                        Logger.LogWarning($"Duplicate {contentType} '{name}' found in comments, the latter one is ignored.", null, path.ToDisplayPath(), context.Source.StartLine.ToString());
                    }
                    else
                    {
                        result.Add(name, description);
                    }
                }
            }

            return result;
        }

        private Dictionary<string, string> GetParameters(XPathNavigator navigator, ITripleSlashCommentParserContext context)
        {
            return GetListContent(navigator, "/member/param", "parameter", context);
        }

        private Dictionary<string, string> GetTypeParameters(XPathNavigator navigator, ITripleSlashCommentParserContext context)
        {
            return GetListContent(navigator, "/member/typeparam", "type parameter", context);
        }

        private void ResolveSeeAlsoCref(XNode node, Action<string> addReference)
        {
            // Resolve <see cref> to <xref>
            ResolveCrefLink(node, "//seealso", addReference);
        }

        private void ResolveSeeCref(XNode node, Action<string> addReference)
        {
            // Resolve <see cref> to <xref>
            ResolveCrefLink(node, "//see", addReference);
        }

        private void ResolveCrefLink(XNode node, string nodeSelector, Action<string> addReference)
        {
            if (node == null || string.IsNullOrEmpty(nodeSelector)) return;

            try
            {
                var nodes = node.XPathSelectElements(nodeSelector + "[@cref]").ToList();
                foreach (var item in nodes)
                {
                    var value = item.Attribute("cref").Value;
                    // Strict check is needed as value could be an invalid href, 
                    // e.g. !:Dictionary&lt;TKey, string&gt; when user manually changed the intellisensed generic type
                    if (CommentIdRegex.IsMatch(value))
                    {
                        value = value.Substring(2);

                        // When see and seealso are top level nodes in triple slash comments, do not convert it into xref node
                        if (item.Parent?.Parent != null)
                        {
                            var replacement = XElement.Parse($"<xref href=\"{WebUtility.HtmlEncode(value)}\" data-throw-if-not-resolved=\"false\"></xref>");
                            item.ReplaceWith(replacement);
                        }

                        if (addReference != null)
                        {
                            addReference(value);
                        }
                    }
                    else
                    {
                        var detailedInfo = new StringBuilder();
                        if (_context != null && _context.Source != null)
                        {
                            if (!string.IsNullOrEmpty(_context.Source.Name))
                            {
                                detailedInfo.Append(" for ");
                                detailedInfo.Append(_context.Source.Name);
                            }
                            if (!string.IsNullOrEmpty(_context.Source.Path))
                            {
                                detailedInfo.Append(" defined in ");
                                detailedInfo.Append(_context.Source.Path);
                                detailedInfo.Append(" Line ");
                                detailedInfo.Append(_context.Source.StartLine);
                            }
                        }

                        Logger.Log(LogLevel.Warning, $"Invalid cref value \"{value}\" found in triple-slash-comments{detailedInfo}, ignored.");
                    }
                }
            }
            catch
            {
            }
        }

        private IEnumerable<string> GetMultipleExampleNodes(XPathNavigator navigator, string selector)
        {
            var iterator = navigator.Select(selector);
            if (iterator == null) yield break;
            foreach (XPathNavigator nav in iterator)
            {
                string description = GetXmlValue(nav);
                yield return description;
            }
        }

        private IEnumerable<CrefInfo> GetMulitpleCrefInfo(XPathNavigator navigator, string selector)
        {
            var iterator = navigator.Clone().Select(selector);
            if (iterator == null) yield break;
            foreach (XPathNavigator nav in iterator)
            {
                string description = GetXmlValue(nav);

                string commentId = nav.GetAttribute("cref", string.Empty);
                if (!string.IsNullOrEmpty(commentId))
                {
                    // Check if exception type is valid and trim prefix
                    if (CommentIdRegex.IsMatch(commentId))
                    {
                        string type = commentId.Substring(2);
                        if (string.IsNullOrEmpty(description)) description = null;
                        yield return new CrefInfo { Description = description, Type = type, CommentId = commentId };
                    }
                }
            }
        }

        private string GetSingleNodeValue(XPathNavigator nav, string selector)
        {
            var node = nav.Clone().SelectSingleNode(selector);
            if (node == null)
            {
                // throw new ArgumentException(selector + " is not found");
                return null;
            }
            else
            {
                var output = GetXmlValue(node);
                return output;
            }
        }

        private string GetXmlValue(XPathNavigator node)
        {
            // NOTE: use node.InnerXml instead of node.Value, to keep decorative nodes,
            // e.g.
            // <remarks><para>Value</para></remarks>
            // decode InnerXml as it encodes
            // IXmlLineInfo.LinePosition starts from 1 and it would ignore '<'
            // e.g.
            // <summary/> the LinePosition is the column number of 's', so it should be minus 2
            var lineInfo = node as IXmlLineInfo;
            int column = lineInfo.HasLineInfo() ? lineInfo.LinePosition - 2 : 0;

            return WebUtility.HtmlDecode(NormalizeXml(node.InnerXml, column));
        }

        /// <summary>
        /// Split xml into lines. Trim meaningless whitespaces.
        /// if a line starts with xml node, all leading whitespaces would be trimmed
        /// otherwise text node start position always aligns with the start position of its parent line(the last previous line that starts with xml node)
        /// Trim newline character for code element.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="parentIndex">the start position of the last previous line that starts with xml node</param>
        /// <returns>normalized xml</returns>
        private static string NormalizeXml(string xml, int parentIndex)
        {
            var lines = LineBreakRegex.Split(xml);
            var normalized = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    normalized.Add(string.Empty);
                }
                else
                {
                    // TO-DO: special logic for TAB case
                    int index = line.TakeWhile(char.IsWhiteSpace).Count();
                    if (line[index] == '<')
                    {
                        parentIndex = index;
                    }

                    normalized.Add(line.Substring(Math.Min(parentIndex, index)));
                }
            }

            // trim newline character for code element
            return CodeElementRegex.Replace(string.Join("\n", normalized), m => { var group = m.Groups[1]; return m.Value.Replace(group.ToString(), group.ToString().Trim('\n')); });
        }
    }
}
