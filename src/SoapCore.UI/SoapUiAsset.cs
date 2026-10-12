using System;
using System.Security.Cryptography;
using Microsoft.Net.Http.Headers;

namespace SoapCore.UI
{
	/// <summary>
	/// One embedded UI file, loaded once and served from memory. Immutable.
	/// </summary>
	internal sealed class SoapUiAsset
	{
		public SoapUiAsset(string contentType, byte[] bytes)
		{
			ContentType = contentType;
			Bytes = bytes;

			// Strong ETag: the first 16 hex characters of the SHA-256 of the content.
			var hash = SHA256.HashData(bytes);
			ETag = new EntityTagHeaderValue("\"" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant() + "\"");
		}

		public string ContentType { get; }

		public byte[] Bytes { get; }

		public EntityTagHeaderValue ETag { get; }
	}
}
