using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SoapCore.Tests.Ui
{
	// A temporary XML documentation file for a test. Disposing it deletes the file.
	public sealed class UiTempXmlFile : IDisposable
	{
		public const string FilePrefix = "soapcore-ui-docs-";

		private UiTempXmlFile(string filePath)
		{
			FilePath = filePath;
		}

		public string FilePath { get; }

		// A path in the temporary directory that no other test uses. The name starts with FilePrefix, then the given text.
		public static string NewPath(string kind)
		{
			return Path.Combine(Path.GetTempPath(), FilePrefix + kind + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".xml");
		}

		// Writes the text exactly as given.
		public static UiTempXmlFile Create(string content)
		{
			var filePath = NewPath(string.Empty);
			File.WriteAllText(filePath, content, new UTF8Encoding(false));

			return new UiTempXmlFile(filePath);
		}

		// Writes a documentation file in the format the compiler produces, with the given member elements.
		public static UiTempXmlFile CreateDoc(params string[] members)
		{
			return Create(Doc(members));
		}

		public static string Doc(params string[] members)
		{
			return "<?xml version=\"1.0\"?>\n<doc>\n<assembly><name>SoapCore.Tests</name></assembly>\n<members>\n" + string.Join("\n", members) + "\n</members>\n</doc>\n";
		}

		// One member element. The body parts are joined into its inner XML, for example "<summary>Text.</summary>", "<param name=\"x\">The x.</param>".
		public static string Member(string id, params string[] bodyParts)
		{
			return "<member name=\"" + id + "\">" + string.Concat(bodyParts) + "</member>";
		}

		public void Dispose()
		{
			try
			{
				File.Delete(FilePath);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				// A temporary file that stays behind is not worth failing a test.
			}
		}
	}
}
