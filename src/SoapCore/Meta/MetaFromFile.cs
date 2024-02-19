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
			var xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(xmlString);

			foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
			{
				if (node.Prefix == xmlDoc.DocumentElement.Prefix && node.LocalName == "import")
				{
					if (WSDLFolder != null)
					{
						if (node.Attributes["location"] == null)
						{
							node.Attributes.Append(xmlDoc.CreateAttribute("location"));
						}

						string name = node.Attributes["location"].InnerText;
						node.Attributes["location"].InnerText = WebServiceLocation() + "?import&name=" + name.Replace("./", string.Empty);
					}
				}

				if (node.Prefix == xmlDoc.DocumentElement.Prefix && node.LocalName == "types")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.LocalName == "schema")
						{
							foreach (XmlNode importOrIncludeNode in schemaNode.ChildNodes)
							{
								if (importOrIncludeNode.LocalName == "import" || importOrIncludeNode.LocalName == "include")
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
			var xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(xmlString);

			foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
			{
				if (node.LocalName == "import" || node.LocalName == "include")
				{
					string name = node.Attributes["schemaLocation"].InnerText;
					node.Attributes["schemaLocation"].InnerText = SchemaLocation() + "&name=" + name.Replace("./", string.Empty);
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
