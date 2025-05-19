using System;
using System.Buffers;
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

		public static string GetSoapAction(HttpContext httpContext, ref Message message)
		{
			var soapAction = httpContext.Request.Headers["SOAPAction"].FirstOrDefault().AsSpan();
			if (soapAction.Length == 2 && soapAction[0] == '"' && soapAction[1] == '"')
			{
				soapAction = ReadOnlySpan<char>.Empty;
			}

			if (soapAction.IsEmpty)
			{
#if NET8_0_OR_GREATER

				foreach (var item in httpContext.Request.Headers["Content-Type"])
				{
					if (string.IsNullOrWhiteSpace(item))
					{
						continue;
					}

					var itemSpan = item.AsSpan();
					var nInstances = 1;

					for (int ci = 0; ci < itemSpan.Length; ci++)
					{
						for (int cti = 0; cti < ContentTypeSeparators.Length; cti++)
						{
							if (itemSpan[ci] == ContentTypeSeparators[cti])
							{
								nInstances++;
							}
						}
					}

					var buff = ArrayPool<Range>.Shared.Rent(nInstances);
					var rangeSpan = new Span<Range>(buff);
					var nItems = itemSpan.Split(rangeSpan, ContentTypeSeparators, StringSplitOptions.RemoveEmptyEntries);

					for (int i = 0; i < nItems; i++)
					{
						var headerItem = itemSpan[rangeSpan[i]].TrimStart();
						if (headerItem.Length < 6)
						{
							continue;
						}

						if (!headerItem.StartsWith("action".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						headerItem = headerItem.Slice(6).TrimStart();

						if (headerItem[0] != '=')
						{
							continue;
						}

						headerItem = headerItem.Slice(1).TrimStart();

						if (headerItem[0] != '"')
						{
							continue;
						}

						headerItem = headerItem.Slice(1).TrimStart();

						var quoteIndex = headerItem.IndexOf('"');
						if (quoteIndex < 0)
						{
							continue;
						}

						ArrayPool<Range>.Shared.Return(buff);
						soapAction = headerItem.Slice(0, quoteIndex);
					}

					ArrayPool<Range>.Shared.Return(buff);
				}

#else
				var contentTypes = GetContentTypes(httpContext);
				foreach (string headerItemStr in contentTypes)
				{
					var headerItem = headerItemStr.AsSpan();
					if (headerItem.Length < 6)
					{
						continue;
					}

					if (!headerItem.StartsWith("action".AsSpan(), StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					headerItem = headerItem.Slice(6).TrimStart();

					if (headerItem[0] != '=')
					{
						continue;
					}

					headerItem = headerItem.Slice(1).TrimStart();

					if (headerItem[0] != '"')
					{
						continue;
					}

					headerItem = headerItem.Slice(1).TrimStart();

					var quoteIndex = headerItem.IndexOf('"');
					if (quoteIndex < 0)
					{
						continue;
					}

					soapAction = headerItem.Slice(0, quoteIndex);
				}
#endif

				if (soapAction != null &&
				    (GetTrimmedSoapAction(soapAction).Length == 0 || GetTrimmedClearedSoapAction(soapAction).Length == 0))
				{
					soapAction = ReadOnlySpan<char>.Empty;
				}

				if (soapAction.IsEmpty)
				{
					if (!string.IsNullOrEmpty(message.Headers.Action))
					{
						soapAction = message.Headers.Action.AsSpan();
					}

					if (soapAction.IsEmpty)
					{
						var headerInfo = message.Headers.FirstOrDefault(h => h.Name.Equals("action", StringComparison.OrdinalIgnoreCase));

						if (headerInfo != null)
						{
							soapAction = message.Headers.GetHeader<string>(headerInfo.Name, headerInfo.Namespace).AsSpan();
						}
					}
				}

				if (soapAction.IsEmpty)
				{
					XmlDictionaryReader reader = null;
					if (!message.IsEmpty)
					{
						MessageBuffer mb = message.CreateBufferedCopy(int.MaxValue);
						Message responseMsg = mb.CreateMessage();
						message = mb.CreateMessage();

						reader = responseMsg.GetReaderAtBodyContents();
						soapAction = reader.LocalName.AsSpan();
					}
				}
			}

			if (!soapAction.IsEmpty)
			{
				// soapAction may have '"' in some cases.
				soapAction = soapAction.Trim('"');
			}

			return soapAction.ToString();
		}

		public static ReadOnlySpan<char> GetTrimmedClearedSoapAction(ReadOnlySpan<char> inSoapAction)
		{
			ReadOnlySpan<char> trimmedAction = GetTrimmedSoapAction(inSoapAction);

			if (trimmedAction.EndsWith("Request".AsSpan()))
			{
				var clearedAction = trimmedAction.Slice(0, trimmedAction.Length - 7);
				return clearedAction;
			}

			return trimmedAction;
		}

		public static ReadOnlySpan<char> GetTrimmedSoapAction(ReadOnlySpan<char> inSoapAction)
		{
			ReadOnlySpan<char> soapAction = inSoapAction;
			var lastIndexOfSlash = soapAction.LastIndexOf('/');
			if (lastIndexOfSlash >= 0)
			{
				// soapAction may be a path. Therefore must take the action from the path provided.
				soapAction = soapAction.Slice(lastIndexOfSlash + 1);
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
