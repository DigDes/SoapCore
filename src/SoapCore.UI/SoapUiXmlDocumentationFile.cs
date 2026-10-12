using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace SoapCore.UI
{
	/// <summary>
	/// The descriptions read from one XML documentation file: the plain text of the <c>summary</c> of every type and method
	/// and of the <c>param</c> of every method, by member id. Only those are kept, as cleaned plain text, so the file itself is not retained.
	/// The file is read as untrusted input: DTDs are refused, no external resource is resolved and the size is capped.
	/// </summary>
	internal sealed class SoapUiXmlDocumentationFile
	{
		// Characters, not bytes. The XML documentation of a big framework assembly is a few megabytes.
		private const int MaxCharactersInDocument = 20 * 1024 * 1024;

		// Raw text collected for one element before it is cleaned. The cleaned text is much shorter (see SoapUiDescriptionText.MaxLength).
		private const int MaxRawTextLength = 16 * 1024;

		private readonly Dictionary<string, Member> _members;

		private SoapUiXmlDocumentationFile(Dictionary<string, Member> members)
		{
			_members = members;
		}

		/// <summary>
		/// Reads a documentation file.
		/// </summary>
		/// <param name="path">The path of the file.</param>
		/// <returns>The descriptions of the file.</returns>
		/// <exception cref="IOException">The file cannot be opened or read.</exception>
		/// <exception cref="XmlException">The file is not well-formed XML, declares a DTD, or is too big.</exception>
		public static SoapUiXmlDocumentationFile Load(string path)
		{
			var settings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null,
				MaxCharactersInDocument = MaxCharactersInDocument,
				IgnoreComments = true,
				IgnoreProcessingInstructions = true,
			};

			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
			using var reader = XmlReader.Create(stream, settings);

			var members = new Dictionary<string, Member>(StringComparer.Ordinal);
			while (!reader.EOF)
			{
				if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "member")
				{
					ReadMember(reader, members);
				}
				else
				{
					reader.Read();
				}
			}

			return new SoapUiXmlDocumentationFile(members);
		}

		/// <summary>
		/// Gets the summary of a type or method.
		/// </summary>
		/// <param name="memberId">The member id, see <see cref="SoapUiXmlDocIds"/>.</param>
		/// <returns>The plain text of the <c>summary</c>, or <c>null</c>.</returns>
		public string? GetSummary(string memberId)
		{
			return _members.TryGetValue(memberId, out var member) ? member.Summary : null;
		}

		/// <summary>
		/// Gets the description of a parameter of a method.
		/// </summary>
		/// <param name="memberId">The member id of the method.</param>
		/// <param name="parameterName">The name of the parameter, as in the <c>name</c> attribute of the <c>param</c> element.</param>
		/// <returns>The plain text of the <c>param</c>, or <c>null</c>.</returns>
		public string? GetParameter(string memberId, string parameterName)
		{
			if (_members.TryGetValue(memberId, out var member) && member.Parameters != null && member.Parameters.TryGetValue(parameterName, out var text))
			{
				return text;
			}

			return null;
		}

		// The reader is on a "member" element. Always leaves it on the node after that element.
		private static void ReadMember(XmlReader reader, Dictionary<string, Member> members)
		{
			var id = reader.GetAttribute("name");
			if (string.IsNullOrEmpty(id) || !(id.StartsWith("T:", StringComparison.Ordinal) || id.StartsWith("M:", StringComparison.Ordinal)))
			{
				reader.Skip();
				return;
			}

			var depth = reader.Depth;
			var isEmpty = reader.IsEmptyElement;
			reader.Read();
			if (isEmpty)
			{
				return;
			}

			string? summary = null;
			Dictionary<string, string>? parameters = null;
			while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
			{
				if (reader.NodeType != XmlNodeType.Element || reader.Depth != depth + 1)
				{
					reader.Read();
				}
				else if (reader.LocalName == "summary" && summary == null)
				{
					summary = ReadText(reader);
				}
				else if (reader.LocalName == "param")
				{
					var name = reader.GetAttribute("name");
					var text = ReadText(reader);
					if (!string.IsNullOrEmpty(name) && text != null)
					{
						parameters ??= new Dictionary<string, string>(StringComparer.Ordinal);
						parameters[name] = text;
					}
				}
				else
				{
					reader.Skip();
				}
			}

			// The end element of the member.
			reader.Read();

			if (summary != null || parameters != null)
			{
				members[id] = new Member(summary, parameters);
			}
		}

		// The reader is on an element. Returns its text as plain text and leaves the reader on the node after the element.
		private static string? ReadText(XmlReader reader)
		{
			var depth = reader.Depth;
			var isEmpty = reader.IsEmptyElement;
			reader.Read();
			if (isEmpty)
			{
				return null;
			}

			var builder = new StringBuilder();
			while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
			{
				if (builder.Length < MaxRawTextLength)
				{
					AppendNode(reader, builder);
				}

				reader.Read();
			}

			// The end element of the text element.
			reader.Read();

			return SoapUiDescriptionText.Normalize(builder.ToString());
		}

		private static void AppendNode(XmlReader reader, StringBuilder builder)
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					builder.Append(reader.Value);
					break;
				case XmlNodeType.Element:
					AppendElement(reader, builder);
					break;
				case XmlNodeType.EndElement:
					if (IsBlock(reader.LocalName))
					{
						builder.Append(' ');
					}

					break;
			}
		}

		// Only the words of a reference are kept: no markup of any kind is produced.
		// <see cref="X"/> is "X". With content, <see cref="X">text</see>, the content is the text.
		private static void AppendElement(XmlReader reader, StringBuilder builder)
		{
			switch (reader.LocalName)
			{
				case "see":
				case "seealso":
					if (reader.IsEmptyElement)
					{
						var cref = reader.GetAttribute("cref");
						builder.Append(cref != null ? SoapUiDescriptionText.CrefName(cref) : reader.GetAttribute("langword") ?? reader.GetAttribute("href"));
					}

					break;
				case "paramref":
				case "typeparamref":
					builder.Append(reader.GetAttribute("name"));
					break;
				default:
					if (IsBlock(reader.LocalName))
					{
						builder.Append(' ');
					}

					break;
			}
		}

		private static bool IsBlock(string name)
		{
			switch (name)
			{
				case "para":
				case "br":
				case "list":
				case "listheader":
				case "item":
				case "term":
				case "description":
				case "code":
				case "example":
				case "remarks":
					return true;
				default:
					return false;
			}
		}

		private sealed class Member
		{
			public Member(string? summary, Dictionary<string, string>? parameters)
			{
				Summary = summary;
				Parameters = parameters;
			}

			public string? Summary { get; }

			public Dictionary<string, string>? Parameters { get; }
		}
	}
}
