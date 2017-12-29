/* Лицензионное соглашение на использование набора средств разработки
 * «SDK Яндекс.Диска» доступно по адресу: http://legal.yandex.ru/sdk_agreement
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Disk.SDK.Utils
{
    /// <summary>
    /// Represents the parser for response's results.
    /// </summary>
    internal static class ResponseParser
    {
        /// <summary>
        /// Parses the disk item.
        /// </summary>
        /// <param name="currentPath">The current path.</param>
        /// <param name="responseText">The response text.</param>
        /// <returns>The  parsed item.</returns>
        public static DiskItemInfo ParseItem(string currentPath, string responseText)
        {
            return ParseItems(currentPath, responseText).FirstOrDefault();
        }

        /// <summary>
        /// Parses the disk items.
        /// </summary>
        /// <param name="currentPath">The current path.</param>
        /// <param name="responseText">The response text.</param>
        /// <returns>The list of parsed items.</returns>
        public static IEnumerable<DiskItemInfo> ParseItems(string currentPath, string responseText)
        {
            var items = new List<DiskItemInfo>();
            var xmlBytes = Encoding.UTF8.GetBytes(responseText);
            using (var xmlStream = new MemoryStream(xmlBytes))
            {
                using (var reader = XmlReader.Create(xmlStream))
                {
                    DiskItemInfo itemInfo = null;
                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name)
                            {
                                case "d:response":
                                    itemInfo = new DiskItemInfo();
                                    break;
                                case "d:href":
                                    reader.Read();
                                    itemInfo.FullPath = reader.Value;
                                    break;
                                case "d:creationdate":
                                    reader.Read();
                                    itemInfo.CreationDate = new DateTime();
                                    if (DateTime.TryParse(reader.Value, out DateTime creationdate))
                                    {
                                        itemInfo.CreationDate = creationdate;
                                    }
                                    break;
                                case "d:getlastmodified":
                                    reader.Read();
                                    itemInfo.LastModified = new DateTime();
                                    if(DateTime.TryParse(reader.Value, out DateTime lastmodified))
                                    {
                                        itemInfo.LastModified = lastmodified;
                                    }
                                    break;
                                case "d:displayname":
                                    reader.Read();
                                    itemInfo.DisplayName = reader.Value;
                                    break;
                                case "d:getcontentlength":
                                    reader.Read();
                                    itemInfo.ContentLength = 0;
                                    if (ulong.TryParse(reader.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out ulong length))
                                    {
                                        itemInfo.ContentLength = length;
                                    }
                                    break;
                                case "d:getcontenttype":
                                    reader.Read();
                                    itemInfo.ContentType = reader.Value;
                                    break;
                                case "d:getetag":
                                    reader.Read();
                                    itemInfo.Etag = reader.Value;
                                    break;
                                case "d:collection":
                                    itemInfo.IsDirectory = true;
                                    break;
                                case "public_url":
                                    reader.Read();
                                    itemInfo.PublicUrl = reader.Value;
                                    break;
                            }
                        }
                        else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "d:response")
                        {
                            if (itemInfo.OriginalFullPath != currentPath)
                            {
                                items.Add(itemInfo);
                            }
                        }
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Parses the link.
        /// </summary>
        /// <param name="responseText">The response text.</param>
        /// <returns>The parsed link.</returns>
        public static string ParseLink(string responseText)
        {
            var xmlBytes = Encoding.UTF8.GetBytes(responseText);
            using (var xmlStream = new MemoryStream(xmlBytes))
            {
                using (var reader = XmlReader.Create(xmlStream))
                {
                    reader.ReadToFollowing("public_url");
                    var url = reader.ReadElementContentAsString();
                    return url;
                }
            }
        }

        /// <summary>
        /// Parses the token.
        /// </summary>
        /// <param name="responseStream">The response stream.</param>
        /// <returns>The parsed access token.</returns>
        public static string ParseToken(Stream responseStream)
        {
            using (var reader = new StreamReader(responseStream))
            {
                var responseText = reader.ReadToEnd();
                return ParseToken(responseText);
            }
        }

        /// <summary>
        /// Parses the token.
        /// </summary>
        /// <param name="resultString">The result string.</param>
        /// <returns>The parsed access token.</returns>
        public static string ParseToken(string resultString)
        {
            return Regex.Match(resultString, WebdavResources.TokenRegexPattern).Value;
        }

        /// <summary>
        /// Parses the disk space.
        /// </summary>
        /// <param name="responseText">The result string.</param>
        /// <returns>The parsed access token.</returns>
        public static Tuple<ulong,ulong> ParseDiskSapce(string responseText)
        {
            ulong available = 0;
            ulong used = 0;
            var xmlBytes = Encoding.UTF8.GetBytes(responseText);
            using (var xmlStream = new MemoryStream(xmlBytes))
            {
                using (var reader = XmlReader.Create(xmlStream))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name)
                            {
                                case "d:quota-available-bytes":
                                    reader.Read();
                                    available = 0;
                                    if (ulong.TryParse(reader.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out ulong availableSize))
                                    {
                                        available = availableSize;
                                    }
                                    break;
                                case "d:quota-used-bytes":
                                    reader.Read();
                                    used = 0;
                                    if (ulong.TryParse(reader.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out ulong usedSize))
                                    {
                                        used = usedSize;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }

            return new Tuple<ulong, ulong>(used, available);
        }
    }
}