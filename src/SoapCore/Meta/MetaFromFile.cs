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
				if (node.Name == "types")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.Name == "xs:schema")
						{
							foreach (XmlNode importNode in schemaNode.ChildNodes)
							{
								if (importNode.Name == "xs:import")
								{
									string name = importNode.Attributes["schemaLocation"].InnerText;
									importNode.Attributes["schemaLocation"].InnerText = SchemaLocation() + "&name=" + name.Replace("./", string.Empty);
								}
							}
						}
					}
				}

				if (node.Name == "service")
				{
					foreach (XmlNode schemaNode in node.ChildNodes)
					{
						if (schemaNode.Name == "port")
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
				if (node.Name == "xs:import")
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
