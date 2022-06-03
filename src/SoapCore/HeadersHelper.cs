using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel.Channels;
using System.Xml;
using Microsoft.AspNetCore.Http;

namespace SoapCore
{
	internal static class HeadersHelper
	{
		private static readonly char[] ContentTypeSeparators = new[] { ';' };

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetSoapAction(HttpContext httpContext, ref Message message)
		{
			XmlDictionaryReader reader = null;
			if (!message.IsEmpty)
			{
				MessageBuffer mb = message.CreateBufferedCopy(int.MaxValue);
				Message responseMsg = mb.CreateMessage();
				message = mb.CreateMessage();

				reader = responseMsg.GetReaderAtBodyContents();
			}

			var soapAction = httpContext.Request.Headers["SOAPAction"].FirstOrDefault();
			if (soapAction == "\"\"")
			{
				soapAction = string.Empty;
			}

			if (string.IsNullOrEmpty(soapAction))
			{
				var contentTypes = GetContentTypes(httpContext);
				foreach (string headerItem in contentTypes)
				{
					// I want to avoid allocation as possible as I can(I hope to use Span<T> or Utf8String)
					// soap1.2: action name is in Content-Type(like 'action="[action url]"') or body
					int i = 0;

					// skip whitespace
					while (i < headerItem.Length && headerItem[i] == ' ')
					{
						i++;
					}

					if (headerItem.Length - i < 6)
					{
						continue;
					}

					// find 'action'
					if (headerItem[i + 0] == 'a'
						&& headerItem[i + 1] == 'c'
						&& headerItem[i + 2] == 't'
						&& headerItem[i + 3] == 'i'
						&& headerItem[i + 4] == 'o'
						&& headerItem[i + 5] == 'n')
					{
						i += 6;

						// skip white space
						while (i < headerItem.Length && headerItem[i] == ' ')
						{
							i++;
						}

						if (headerItem[i] == '=')
						{
							i++;

							// skip whitespace
							while (i < headerItem.Length && headerItem[i] == ' ')
							{
								i++;
							}

							// action value should be surrounded by '"'
							if (headerItem[i] == '"')
							{
								i++;
								int offset = i;
								while (i < headerItem.Length && headerItem[i] != '"')
								{
									i++;
								}

								if (i < headerItem.Length && headerItem[i] == '"')
								{
									var charray = headerItem.ToCharArray();
									soapAction = new string(charray, offset, i - offset);
									break;
								}
							}
						}
					}
				}

				if (string.IsNullOrEmpty(soapAction))
				{
					if (!string.IsNullOrEmpty(message.Headers.Action))
					{
						soapAction = message.Headers.Action;
					}

					if (string.IsNullOrEmpty(soapAction))
					{
						var headerInfo = message.Headers.FirstOrDefault(h => h.Name.ToLowerInvariant() == "action");

						if (headerInfo != null)
						{
							soapAction = message.Headers.GetHeader<string>(headerInfo.Name, headerInfo.Namespace);
						}
					}
				}

				if (string.IsNullOrEmpty(soapAction) && reader != null)
				{
					soapAction = reader.LocalName;
				}
			}

			if (!string.IsNullOrEmpty(soapAction))
			{
				// soapAction may have '"' in some cases.
				soapAction = soapAction.Trim('"');
			}

			return soapAction;
		}

		public static string GetTrimmedClearedSoapAction(string inSoapAction)
		{
			string trimmedAction = GetTrimmedSoapAction(inSoapAction);

			if (trimmedAction.EndsWith("Request"))
			{
				int endIndex = trimmedAction.LastIndexOf('R');
				string clearedAction = trimmedAction.Substring(0, endIndex);
				return clearedAction;
			}

			return trimmedAction;
		}

		public static string GetTrimmedSoapAction(string inSoapAction)
		{
			string soapAction = inSoapAction;
			if (soapAction.Contains('/'))
			{
				// soapAction may be a path. Therefore must take the action from the path provided.
				soapAction = soapAction.Split('/').Last();
			}

			return soapAction;
		}

		private static IEnumerable<string> GetContentTypes(HttpContext httpContext)
		{
			// in a single header entry is possible to find several content-types separated by ';'
			return httpContext.Request.Headers["Content-Type"]
				.SelectMany(c => c.Split(ContentTypeSeparators, StringSplitOptions.RemoveEmptyEntries));
		}
	}
}
