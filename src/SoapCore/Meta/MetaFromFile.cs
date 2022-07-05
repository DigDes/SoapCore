using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SoapCore.Meta
{
	public class MetaFromFile
	{
		public MetaFromFile()
		{
		}

		/// <summary>
		/// Gets or sets the CurrentWebService
		/// </summary>
		public string CurrentWebService { get; set; }

		/// <summary>
		/// Gets or sets the currentWebServer
		/// </summary>
		public string CurrentWebServer { get; set; }

		/// <summary>
		/// Gets or sets reads and returns the XSDFolder appsetting key form web.config
		/// </summary>
		public string XsdFolder { get; set; }

		/// <summary>
		/// Gets or sets the WSDLFolder appsetting key from web.config
		/// </summary>
		public string WSDLFolder { get; set; }

		/// <summary>
		/// Gets or sets the XSDURL appsetting key from web.config
		/// </summary>
		public string ServerUrl { get; set; }

		[Obsolete]
		public string ReadLocalFile(string path)
		{
			if (!File.Exists(path))
			{
				return string.Empty;
			}

			return File.ReadAllText(path);
		}

#if NETSTANDARD
		public async Task<string> ReadLocalFileAsync(string path)
		{
			if (!File.Exists(path))
			{
				return string.Empty;
			}

			using var reader = File.OpenText(path);
			return await reader.ReadToEndAsync();
		}
#else
		public Task<string> ReadLocalFileAsync(string path)
		{
			if (!File.Exists(path))
			{
				return Task.FromResult(string.Empty);
			}

			return File.ReadAllTextAsync(path);
		}
#endif

		public string ModifyWSDLAddRightSchemaPath(string xmlString)
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(xmlString);

			foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
			{
				if (node.Name == (!string.IsNullOrWhiteSpace(xmlDoc.DocumentElement.Prefix) ? xmlDoc.DocumentElement.Prefix + ":" : xmlDoc.DocumentElement.Prefix) + "types")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.Name == (!string.IsNullOrWhiteSpace(schemaNode.Prefix) ? schemaNode.Prefix + ":" : schemaNode.Prefix) + "schema")
						{
							foreach (XmlNode importOrIncludeNode in schemaNode.ChildNodes)
							{
								if (importOrIncludeNode.Name == ImportNodeName(importOrIncludeNode) || importOrIncludeNode.Name == IncludeNodeName(importOrIncludeNode))
								{
									if (XsdFolder != null)
									{
										if (importOrIncludeNode.Attributes["schemaLocation"] == null)
										{
											importOrIncludeNode.Attributes.Append(xmlDoc.CreateAttribute("schemaLocation"));
										}

										string name = importOrIncludeNode.Attributes["schemaLocation"].InnerText;
										importOrIncludeNode.Attributes["schemaLocation"].InnerText = SchemaLocation() + "&name=" + name.Replace("./", string.Empty);
									}
								}
							}
						}
					}
				}

				if (node.Name == (!string.IsNullOrWhiteSpace(xmlDoc.DocumentElement.Prefix) ? xmlDoc.DocumentElement.Prefix + ":" : xmlDoc.DocumentElement.Prefix) + "service")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.Name == (!string.IsNullOrWhiteSpace(schemaNode.Prefix) ? schemaNode.Prefix + ":" : schemaNode.Prefix) + "port")
						{
							foreach (XmlNode portNode in schemaNode.ChildNodes)
							{
								if (portNode.Name == (!string.IsNullOrWhiteSpace(portNode.Prefix) ? portNode.Prefix + ":" : portNode.Prefix) + "address")
								{
									if (portNode.Attributes["location"] == null)
									{
										portNode.Attributes.Append(xmlDoc.CreateAttribute("location"));
									}

									portNode.Attributes["location"].InnerText = WebServiceLocation();
									break;
								}
							}
						}
					}
				}
			}

			return xmlDoc.InnerXml;
		}

		public string ModifyXSDAddRightSchemaPath(string xmlString)
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(xmlString);

			foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
			{
				if (node.Name == ImportNodeName(node) || node.Name == IncludeNodeName(node))
				{
					string name = node.Attributes["schemaLocation"].InnerText;
					node.Attributes["schemaLocation"].InnerText = SchemaLocation() + "&name=" + name.Replace("./", string.Empty);
				}
			}

			return xmlDoc.InnerXml;
		}

		private static string ImportNodeName(XmlNode node) => (!string.IsNullOrWhiteSpace(node.Prefix) ? node.Prefix + ":" : node.Prefix) + "import";

		private static string IncludeNodeName(XmlNode node) => (!string.IsNullOrWhiteSpace(node.Prefix) ? node.Prefix + ":" : node.Prefix) + "include";

		private string SchemaLocation()
		{
			var schemaLocation = ServerUrl + CurrentWebServer + CurrentWebService + "?xsd";

			return schemaLocation;
		}

		private string WebServiceLocation()
		{
			var webServiceLocation = ServerUrl + CurrentWebServer + CurrentWebService;

			return webServiceLocation;
		}
	}
}
