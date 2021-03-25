using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

		public string ReadLocalFile(string path)
		{
			if (!File.Exists(path))
			{
				return string.Empty;
			}

			// read file
			using var reader = new StreamReader(path);
			var fileContents = reader.ReadToEnd();
			return fileContents;
		}

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
							foreach (XmlNode importNode in schemaNode.ChildNodes)
							{
								if (importNode.Name == (!string.IsNullOrWhiteSpace(importNode.Prefix) ? importNode.Prefix + ":" : importNode.Prefix) + "import")
								{
									if (importNode.Attributes["schemaLocation"] == null)
									{
										importNode.Attributes.Append(xmlDoc.CreateAttribute("schemaLocation"));
									}

									string name = importNode.Attributes["schemaLocation"].InnerText;
									importNode.Attributes["schemaLocation"].InnerText = SchemaLocation() + "&name=" + name.Replace("./", string.Empty);
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
							foreach (XmlNode soapAdressNode in schemaNode.ChildNodes)
							{
								soapAdressNode.Attributes["location"].InnerText = WebServiceLocation();
								break;
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
				if (node.Name == (!string.IsNullOrWhiteSpace(node.Prefix) ? node.Prefix + ":" : node.Prefix) + "import")
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
