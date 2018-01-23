#region Copyright
// 
// DotNetNukeŽ - http://www.dotnetnuke.com
// Copyright (c) 2002-2018
// by DotNetNuke Corporation
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion
#region Usings

using System;
using System.Collections.Generic;
using System.Web;
using System.Xml;

#endregion

namespace DotNetNuke.Services.Syndication
{
    internal class RssXmlHelper
    {
        /// <summary>
        ///   Internal helper class for XML to RSS conversion (and for generating XML from RSS)
        /// </summary>
        /// <param name = "doc"></param>
        /// <returns></returns>
        internal static RssChannelDom ParseChannelXml(XmlDocument doc)
        {
            Dictionary<string, string> channelAttributes = null;
            Dictionary<string, string> imageAttributes = null;
            var itemsAttributesList = new List<Dictionary<string, string>>();

            try
            {
                XmlNode root = doc.DocumentElement;
                if (root.Name == "rss")
                {
                    // RSS
                    for (XmlNode c = root.FirstChild; c != null; c = c.NextSibling)
                    {
                        if (c.NodeType == XmlNodeType.Element && c.Name == "channel")
                        {
                            for (XmlNode n = c.FirstChild; n != null; n = n.NextSibling)
                            {
                                if (n.NodeType == XmlNodeType.Element)
                                {
                                    if (n.Name == "item")
                                    {
                                        itemsAttributesList.Add(ParseAttributesFromXml(n));
                                    }
                                    else if (n.Name == "image")
                                    {
                                        imageAttributes = ParseAttributesFromXml(n);
                                    }
                                }
                            }

                            channelAttributes = ParseAttributesFromXml(c);
                            break;
                        }
                    }
                }
                else if (root.Name == "rdf:RDF")
                {
                    // RDF
                    for (XmlNode n = root.FirstChild; n != null; n = n.NextSibling)
                    {
                        if (n.NodeType == XmlNodeType.Element)
                        {
                            if (n.Name == "channel")
                            {
                                channelAttributes = ParseAttributesFromXml(n);
                            }
                            if (n.Name == "image")
                            {
                                imageAttributes = ParseAttributesFromXml(n);
                            }
                            if (n.Name == "item")
                            {
                                itemsAttributesList.Add(ParseAttributesFromXml(n));
                            }
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unexpected root node");
                }

                if (channelAttributes == null)
                {
                    throw new InvalidOperationException("Cannot find channel node");
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Failed to parse RSS channel", ex);
            }

            return new RssChannelDom(channelAttributes, imageAttributes, itemsAttributesList);
        }

        internal static XmlDocument CreateEmptyRssXml()
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
</rss>");
            return doc;
        }

        internal static XmlNode SaveRssElementAsXml(XmlNode parentNode, RssElementBase element, string elementName)
        {
            XmlDocument doc = parentNode.OwnerDocument;
            XmlNode node = doc.CreateElement(elementName);
            parentNode.AppendChild(node);

            foreach (KeyValuePair<string, string> attr in element.Attributes)
            {
                XmlNode attrNode = doc.CreateElement(attr.Key);
                attrNode.InnerText = ResolveAppRelativeLinkToUrl(attr.Value);
                node.AppendChild(attrNode);
            }

            return node;
        }

        private static Dictionary<string, string> ParseAttributesFromXml(XmlNode node)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (XmlNode n = node.FirstChild; n != null; n = n.NextSibling)
            {
                if (n.NodeType == XmlNodeType.Element && !NodeHasSubElements(n))
                {
                    if (attributes.ContainsKey(n.Name))
                    {
                        attributes[n.Name] = attributes[n.Name] + ", " + n.InnerText.Trim();
                    }
                    else
                    {
                        attributes.Add(n.Name, n.InnerText.Trim());
                    }
                }
            }

            return attributes;
        }

        private static bool NodeHasSubElements(XmlNode node)
        {
            for (XmlNode n = node.FirstChild; n != null; n = n.NextSibling)
            {
                if (n.NodeType == XmlNodeType.Element)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveAppRelativeLinkToUrl(string link)
        {
            if (!string.IsNullOrEmpty(link) && link.StartsWith("~/"))
            {
                HttpContext context = HttpContext.Current;

                if (context != null)
                {
                    string query = null;
                    int iquery = link.IndexOf('?');

                    if (iquery >= 0)
                    {
                        query = link.Substring(iquery);
                        link = link.Substring(0, iquery);
                    }

                    link = VirtualPathUtility.ToAbsolute(link);
                    link = new Uri(context.Request.Url, link).ToString();

                    if (iquery >= 0)
                    {
                        link += query;
                    }
                }
            }

            return link;
        }
    }
}