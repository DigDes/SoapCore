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

		private XmlAttribute EnsureAttribute(XmlDocument xmlDoc, XmlNode node, string attributeName)
		{
			var attribute = node.Attributes[attributeName];
			if (attribute == null)
			{
				attribute = xmlDoc.CreateAttribute(attributeName);
				node.Attributes.Append(attribute);
			}

			return attribute;
		}

		public string ModifyWSDLAddRightSchemaPath(string xmlString)
		{
			var xmlDoc = new XmlDocument() { XmlResolver = null };
			var sr = new StringReader(xmlString);
			var reader = XmlReader.Create(sr, new XmlReaderSettings() { XmlResolver = null });
			xmlDoc.Load(reader);

			foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
			{
				if (WSDLFolder != null && node.Prefix == xmlDoc.DocumentElement.Prefix && node.LocalName == "import")
				{
					var attribute = EnsureAttribute(xmlDoc, node, "location");
					string name = attribute.InnerText.Replace("./", string.Empty);
					attribute.InnerText = WebServiceLocation() + "?import&amp;name=" + name;
				}

				if (XsdFolder != null && node.Prefix == xmlDoc.DocumentElement.Prefix && node.LocalName == "types")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.LocalName == "schema")
						{
							foreach (XmlNode importOrIncludeNode in schemaNode.ChildNodes)
							{
								if ((importOrIncludeNode.LocalName == "import" && importOrIncludeNode.Attributes["schemaLocation"] != null)
								    || importOrIncludeNode.LocalName == "include")
								{
									var attribute = EnsureAttribute(xmlDoc, importOrIncludeNode, "schemaLocation");
									string name = attribute.InnerText.Replace("./", string.Empty);
									attribute.InnerText = SchemaLocation() + "&amp;name=" + name;
								}
							}
						}
					}
				}

				if (node.Prefix == xmlDoc.DocumentElement.Prefix && node.LocalName == "service")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.LocalName == "port")
						{
							foreach (XmlNode portNode in schemaNode.ChildNodes)
							{
								if (portNode.LocalName == "address")
								{
									var attribute = EnsureAttribute(xmlDoc, portNode, "location");
									attribute.InnerText = WebServiceLocation();
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
			var xmlDoc = new XmlDocument() { XmlResolver = null };
			var sr = new StringReader(xmlString);
			var reader = XmlReader.Create(sr, new XmlReaderSettings() { XmlResolver = null });
			xmlDoc.Load(reader);

			foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
			{
				if ((node.LocalName == "import" && node.Attributes["schemaLocation"] != null)
				    || node.LocalName == "include")
				{
					var attribute = EnsureAttribute(xmlDoc, node, "schemaLocation");
					string name = attribute.InnerText.Replace("./", string.Empty);
					attribute.InnerText = SchemaLocation() + "&amp;name=" + name;
				}
			}

			return xmlDoc.InnerXml;
		}

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
