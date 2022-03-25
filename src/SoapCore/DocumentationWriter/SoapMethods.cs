#if !NETCOREAPP3_1_OR_GREATER
using Newtonsoft.Json;
#endif
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		private static XmlElementEventHandler _unknownElementHandler = (sender, args) =>
		{
			if (args.ObjectBeingDeserialized is IElementWithSpecialTransforms deserializedObject)
			{
				deserializedObject.DeserializeElements(args.Element);
			}
			else
			{
				// This will catch so that unknown elements can get logged, if we want to
				// But right now, we ignore them for the documentations sake
			}
		};

		public interface IElementWithSpecialTransforms
		{
			public void DeserializeElements(XmlElement element);
		}

		public static SoapDefinition DeserializeFromFile(string xmlFile)
		{
			XmlSerializer xs = new XmlSerializer(typeof(SoapDefinition));

			xs.UnknownElement += _unknownElementHandler;

			using var sr = new StreamReader(xmlFile);
			return (SoapDefinition)xs.Deserialize(sr);
		}

		public static SoapDefinition DeserializeFromString(string xml)
		{
			XmlSerializer xs = new XmlSerializer(typeof(SoapDefinition));

			xs.UnknownElement += _unknownElementHandler;

			using var sr = new MemoryStream(DefaultEncodings.UTF8.GetBytes(xml));
			return (SoapDefinition)xs.Deserialize(sr);
		}

		public string GenerateDocumentation()
		{
#if NETCOREAPP3_1_OR_GREATER
			var json = System.Text.Json.JsonSerializer.Serialize(this);
#else
			var json = JsonConvert.SerializeObject(this);
#endif

			var html = $@"<!DOCTYPE html>
<html lang=""en"">
	<head>
		<meta charset=""utf-8"">
		<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
		<title>{Service.Name} ({Service.Ports.FirstOrDefault()?.Address.Location}) - Api Documentation</title>
		<link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"" integrity=""sha384-1BmE4kWBq78iYhFldvKuhfTAU6auU8tT94WrHftjDbrCEXSU1oBoqyl2QvZ6jIW3"" crossorigin=""anonymous"" />
		<link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.4.0/styles/default.min.css"" />
		<style>a {{ cursor: pointer; }}</style>
	</head>
	<body>
		<nav class=""navbar navbar-expand-lg navbar-light bg-light"">
			<div class=""container-fluid"">
				<span class=""navbar-brand mb-0 h1"">
					{Service.Name} Documentation
				</span>
			</div>
		</nav>
		<div class=""container-fluid mt-3"" id=""reactRoot""></div>
		<script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"" integrity=""sha384-ka7Sk0Gln4gmtz2MlQnikT1wXgYsOg+OMhuP+IlRH9sENBO0LRn5q+8nbTov4+1p"" crossorigin=""anonymous""></script>
		<script src=""https://unpkg.com/react@17/umd/react.production.min.js"" crossorigin></script>
		<script src=""https://unpkg.com/react-dom@17/umd/react-dom.production.min.js"" crossorigin></script>
		<script type=""text/javascript"">
			window.SoapApiData = {json};

			const soapOldEnvelopeStart = `<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>`;
			const soapOldEnvelopeEnd = `  </soap:Body>
</soap:Envelope>`;
			const soapOldContentType = 'text/xml';

			const soap12EnvelopeStart = `<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>`;
			const soap12EnvelopeEnd = `  </soap12:Body>
</soap12:Envelope>`;
			const soap12ContentType = 'application/soap+xml';

			window.MethodModal = null;

			function GetMethodData(methodName) {{
				const clickedMethod = SoapApiData.PortType.Operations.find(i => i.Name == methodName);

				if(!clickedMethod) return null;

				let inputMessage = null;
				let inputType = null;
				if(!!clickedMethod.Input.Message) {{
					inputMessage = SoapApiData.Messages.find(m => m.Name == escapeName(clickedMethod.Input.Message));

					if(!!inputMessage) {{
						for(const part of inputMessage.Part) {{
							for(const schema of SoapApiData.Types.Schema) {{
								let element = schema.Elements.find(t => t.Name == escapeName(part.Element));
								if(element) {{
									element.ComplexElementType = fetchElementTypesRecursive(element.ComplexElementType);
									inputType = element;
								}}
							}}
						}}
					}}
				}}

				let outputMessage = null;
				let outputType = null;
				if(!!clickedMethod.Output.Message) {{
					outputMessage = SoapApiData.Messages.find(m => m.Name == escapeName(clickedMethod.Output.Message));

					if(!!outputMessage) {{
						for(const part of outputMessage.Part) {{
							for(const schema of SoapApiData.Types.Schema) {{
								let element = schema.Elements.find(t => t.Name == escapeName(part.Element));
								if(element) {{
									element.ComplexElementType = fetchElementTypesRecursive(element.ComplexElementType);
									outputType = element;
								}}
							}}
						}}
					}}
				}}

				return {{
					Method: clickedMethod,
					InputMessage: {{
						Message: inputMessage,
						Type: inputType
					}},
					OutputMessage: {{
						Message: outputMessage,
						Type: outputType
					}}
				}};
			}}

			function getTypeInfo(typeName) {{
			let escapedTypeName = escapeName(typeName);

				for(const schema of SoapApiData.Types.Schema) {{
					let complexType = schema.ComplexTypes.find(t => t.Name == escapedTypeName);
					if(complexType)
						return complexType;

					let simpleType = schema.SimpleTypes.find(t => t.Name == escapedTypeName);
					if(simpleType)
						return simpleType;
				}}

				return null;
			}}

			function getSchemaForType(typeName) {{
				let escapedTypeName = escapeName(typeName);

				for(const schema of SoapApiData.Types.Schema) {{
					let complexType = schema.ComplexTypes.find(t => t.Name == escapedTypeName);
					if(complexType)
						return schema;

					let simpleType = schema.SimpleTypes.find(t => t.Name == escapedTypeName);
					if(simpleType)
						return schema;

					let elementType = schema.Elements.find(t => t.Name == escapedTypeName);
					if(elementType)
						return schema;
				}}

				return null;
			}}

			function fetchElementTypesRecursive(cet) {{
				if(cet && cet.TypeInformation && cet.TypeInformation.Element) {{
					for(const elementItem of cet.TypeInformation.Element) {{
						if(elementItem.Type == null || elementItem.Type.startsWith('xsd:')) {{
							continue;
						}} else {{
							elementItem.TypeInformation =
								fetchElementTypesRecursive(
									getTypeInfo(elementItem.Type)
								);
						}}
					}}
				}}

				return cet;
			}}

			function escapeName(input) {{
				return input.indexOf(':') > -1 ? input.split(':')[1] : input;
			}}

			function ApiMethodModal(methodName) {{
				const methodData = GetMethodData(methodName);

				let modalTitle = document.querySelector('#methodModal .modal-title');
				modalTitle.innerText = `Method: ${{methodData.Method.Name}}`;

				const bindingData = [];

				for(let port of SoapApiData.Service.Ports) {{
					let namespace = port.Address.Namespace;
					let bindingMethod = SoapApiData.Bindings.find(b => b.Name == port.Name).Operations.find(o => o.Name == methodName);
					if(bindingMethod) {{
						bindingData.push({{ namespace, method: bindingMethod, executionMethod: methodData, address: port.Address.Location }});
					}}
				}}

				let invocationHTML = '';

				for(let binding of bindingData) {{
					let bindingHTML = '<div>';
					if(binding.namespace.endsWith('/soap/')) {{
						bindingHTML += `<h6>SOAP 1.1</h6>
The following is a sample SOAP 1.1 request and response.<br />
The <code>placeholders</code> shown need to be replaced with actual values.<br /><br />`;

						bindingHTML += generateSampleCallAndResult(
							binding,
							soapOldContentType,
							soapOldEnvelopeStart,
							soapOldEnvelopeEnd
						);
					}} else if (binding.namespace.endsWith('/soap12/')) {{
						bindingHTML += `<h6>SOAP 1.2</h6>
The following is a sample SOAP 1.2 request and response.<br />
The <code>placeholders</code> shown need to be replaced with actual values.<br /><br />`;

						bindingHTML += generateSampleCallAndResult(
							binding,
							soap12ContentType,
							soap12EnvelopeStart,
							soap12EnvelopeEnd
						);
					}} else {{
						bindingHTML += `<h6>${{binding.namespace}}</h6>
<pre><code class=""language-json"">{{JSON.stringify(binding, null, 2)}}</code></pre>`
					}}

					bindingHTML += '<hr /></div>';

					invocationHTML += bindingHTML;
				}}

				let modalBody = document.querySelector('#methodModal .modal-body');
				modalBody.innerHTML = invocationHTML;

				hljs.highlightAll();

				document.getElementById('methodModal').addEventListener('hidden.bs.modal', function() {{
					location.hash = '';
				}});
				window.MethodModal.show();
			}}

			function generateSampleCallAndResult(binding, contentType, envelopeStart, envelopeEnd) {{
				const apiUrl = new URL(binding.address);

				let actionHeader = contentType == 'text/xml' ? `
SOAPAction: ""${{binding.method.Operation.SoapAction}}""` : '';

				let generatedRequest = generateXmlFromType(binding.executionMethod.InputMessage.Type.Name, binding.executionMethod.InputMessage.Type.ComplexElementType, 2, getSchemaForType(binding.executionMethod.InputMessage.Type.Name).TargetNamespace, true);
				let generatedResponse = generateXmlFromType(binding.executionMethod.OutputMessage.Type.Name, binding.executionMethod.OutputMessage.Type.ComplexElementType, 2, getSchemaForType(binding.executionMethod.OutputMessage.Type.Name).TargetNamespace, true);

				return `
<b>Headers</b><br />
<pre><code class=""language-http"">POST ${{escapeHtml(apiUrl.pathname)}} HTTP/1.1
Host: ${{escapeHtml(apiUrl.hostname)}}
Content-Type: ${{escapeHtml(contentType)}}; charset=utf-8${{escapeHtml(actionHeader)}}
</code></pre>
<b>Request Body</b><br />
<pre><code class=""language-xml"">${{escapeHtml(envelopeStart)}}
${{escapeHtml(generatedRequest)}}${{escapeHtml(envelopeEnd)}}</code></pre>
<b>Response Body</b><br />
<pre><code class=""language-xml"">${{escapeHtml(envelopeStart)}}
${{escapeHtml(generatedResponse)}}${{escapeHtml(envelopeEnd)}}</code></pre>`;
			}}

			function generateXmlFromType(elementName, messageType, depth, targetNamespace, newLine) {{
				if(
					(messageType.Type && messageType.Type.indexOf('xsd:') == 0) ||
					(messageType.Type && messageType.Type.indexOf('s:') == 0) ||
					(messageType.TypeInformation && messageType.TypeInformation.Restriction)) {{
					newLine = false;
				}}

				const spaceCharacter = ' '.repeat(depth * 2);
				let xml = `${{spaceCharacter}}<${{elementName}}` +
					(!!targetNamespace ? ' xmlns=""' + targetNamespace + '""' : '') +
					'>' + (newLine ? '\n' : '');

				let subElement = null;

				if(messageType.TypeInformation && messageType.TypeInformation.TypeInformation) {{
					subElement = messageType.TypeInformation;
				}} else if (messageType.TypeInformation && !messageType.TypeInformation.TypeInformation) {{
					subElement = messageType;
				}}

				if(subElement) {{
					if(subElement.TypeInformation.Element) {{
						for(let typeElement of subElement.TypeInformation.Element) {{
							xml += generateXmlFromType(typeElement.Name, typeElement, depth + 1, null, true);
						}}
					}} else if(subElement.TypeInformation.Restriction) {{
						xml += subElement.TypeInformation.Restriction.EnumerationValue.map(r => r.Value).join(' or ');
					}} else {{
						console.error('UNHANDLED', subElement.TypeInformation);
					}}
				}} else {{
					if(messageType.Type) {{
						xml += messageType.Type.replace('xsd:', '').replace('s:', '');
					}}
				}}

				xml += `${{newLine ? spaceCharacter : ''}}</${{elementName}}>\n`;
				return xml;
			}}

			function escapeHtml(html) {{
				const tagsToReplace = {{
					'&': '&amp;',
					'<': '&lt;',
					'>': '&gt;'
				}};
				return html.replace(/[&<>]/g, function(tag) {{
					return tagsToReplace[tag] || tag;
				}});
			}}

			function hashRouter() {{
				if(window.MethodModal == null) {{
					window.MethodModal = new bootstrap.Modal(document.getElementById('methodModal'))
				}}

				let hashValue = location.hash.substr(1);

				if(hashValue.length == 0) {{ hashValue = '_index'; }}

				if(hashValue == '_index') {{
					if(window.MethodModal) {{
						window.MethodModal.hide();
					}}
				}} else {{
					let method = GetMethodData(hashValue);
					if(method != null) {{
						ApiMethodModal(hashValue);
					}}
				}}
			}}

			window.addEventListener('DOMContentLoaded', hashRouter);
			window.addEventListener('hashchange', hashRouter);
		</script>
		<script type=""text/javascript"">
			""use strict"";

function App() {{
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(""em"", null, ""This documentation is automatically generated from the "", /*#__PURE__*/React.createElement(""a"", {{
    href: SoapApiData.Service.Ports[0].Address.Location + ""?wsdl"",
    target: ""_blank""
  }}, ""WSDL""), "".""), /*#__PURE__*/React.createElement(""hr"", null), /*#__PURE__*/React.createElement(""h1"", null, ""Endpoints / Bindings""), /*#__PURE__*/React.createElement(""div"", {{
    class: ""table-responsive""
  }}, /*#__PURE__*/React.createElement(""table"", {{
    class: ""table table-striped table-hover""
  }}, /*#__PURE__*/React.createElement(""thead"", null, /*#__PURE__*/React.createElement(""tr"", null, /*#__PURE__*/React.createElement(""th"", null, ""Name""), /*#__PURE__*/React.createElement(""th"", null, ""Binding""), /*#__PURE__*/React.createElement(""th"", null, ""Address""), /*#__PURE__*/React.createElement(""th"", null, ""Namespace""))), /*#__PURE__*/React.createElement(""tbody"", null, SoapApiData.Service.Ports.map(function (p) {{
    return /*#__PURE__*/React.createElement(""tr"", null, /*#__PURE__*/React.createElement(""td"", null, p.Name), /*#__PURE__*/React.createElement(""td"", null, p.Binding), /*#__PURE__*/React.createElement(""td"", null, p.Address.Location), /*#__PURE__*/React.createElement(""td"", null, p.Address.Namespace));
  }})))), /*#__PURE__*/React.createElement(""hr"", null), /*#__PURE__*/React.createElement(""h1"", null, ""Methods""), /*#__PURE__*/React.createElement(""ul"", null, SoapApiData.PortType.Operations.sort(function (a, b) {{
    if (a.Name < b.Name) return -1;
    return 1;
  }}).map(function (p) {{
    return /*#__PURE__*/React.createElement(""li"", null, /*#__PURE__*/React.createElement(""a"", {{
      href: ""#"" + p.Name,
      class: ""link-primary"",
      onClick: function onClick() {{
        return ApiMethodModal(p.Name);
      }}
    }}, p.Name));
  }})), /*#__PURE__*/React.createElement(ModalComponent, null));
}}

;

function ModalComponent() {{
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(""div"", {{
    class: ""modal fade"",
    id: ""methodModal"",
    tabindex: ""-1"",
    ""aria-hidden"": ""true""
  }}, /*#__PURE__*/React.createElement(""div"", {{
    class: ""modal-dialog modal-dialog-centered modal-dialog-scrollable modal-fullscreen""
  }}, /*#__PURE__*/React.createElement(""div"", {{
    class: ""modal-content""
  }}, /*#__PURE__*/React.createElement(""div"", {{
    class: ""modal-header""
  }}, /*#__PURE__*/React.createElement(""h5"", {{
    class: ""modal-title""
  }}, ""Title""), /*#__PURE__*/React.createElement(""button"", {{
    type: ""button"",
    class: ""btn-close"",
    ""data-bs-dismiss"": ""modal"",
    ""aria-label"": ""Close""
  }})), /*#__PURE__*/React.createElement(""div"", {{
    class: ""modal-body""
  }}, ""Body"")))));
}}

ReactDOM.render( /*#__PURE__*/React.createElement(App, null), document.getElementById('reactRoot'));
		</script>
		<script src=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.4.0/highlight.min.js""></script>
		<script src=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.4.0/languages/http.min.js""></script>
		<script type=""text/javascript"">hljs.highlightAll();</script>
	</body>
</html>";

			return html;
		}
	}
}
