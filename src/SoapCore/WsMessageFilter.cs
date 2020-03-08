using System;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Xml.Serialization;
using SoapCore.Extensibility;
using static System.Convert;
using static System.Text.Encoding;

namespace SoapCore
{
	public class WsMessageFilter : IMessageFilter
	{
		private const string _passwordTextType = @"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";
		private readonly string _username;
		private readonly string _password;
		private readonly string _authMissingErrorMessage = "Referenced security token could not be retrieved";
		private readonly string _authInvalidErrorMessage = "Authentication error: Authentication failed: the supplied credential is not right";

		public WsMessageFilter(string username, string password)
		{
			_username = username;
			_password = password;
		}

		public WsMessageFilter(string username, string password, string authMissingErrorMessage, string authInvalidErrorMessage)
		{
			_username = username;
			_password = password;
			_authMissingErrorMessage = authMissingErrorMessage;
			_authInvalidErrorMessage = authInvalidErrorMessage;
		}

		public void OnRequestExecuting(Message message)
		{
			WsUsernameToken wsUsernameToken;
			try
			{
				wsUsernameToken = GetWsUsernameToken(message);
			}
			catch (Exception)
			{
				throw new AuthenticationException(_authMissingErrorMessage);
			}

			if (!ValidateWsUsernameToken(wsUsernameToken))
			{
				throw new InvalidCredentialException(_authInvalidErrorMessage);
			}
		}

		public void OnResponseExecuting(Message message)
		{
			//empty
		}

		private WsUsernameToken GetWsUsernameToken(Message message)
		{
			WsUsernameToken wsUsernameToken = null;
			for (var i = 0; i < message.Headers.Count; i++)
			{
				if (message.Headers[i].Name.ToLower() == "security")
				{
					using var reader = message.Headers.GetReaderAtHeader(i);
					reader.Read();
					var serializer = new XmlSerializer(typeof(WsUsernameToken));
					wsUsernameToken = (WsUsernameToken)serializer.Deserialize(reader);
				}
			}

			if (wsUsernameToken == null)
			{
				throw new Exception();
			}

			if (wsUsernameToken.Nonce != null ^ wsUsernameToken.Created != null)
			{
				throw new Exception();
			}

			return wsUsernameToken;
		}

		private bool ValidateWsUsernameToken(WsUsernameToken wsUsernameToken)
		{
			if (wsUsernameToken.Username != _username)
			{
				return false;
			}

			var isClearText = wsUsernameToken.Password?.Type == null || wsUsernameToken.Password.Type == _passwordTextType;
			if (isClearText)
			{
				return wsUsernameToken.Password?.Value == _password;
			}

			var nonceArray = wsUsernameToken.Nonce != null ? wsUsernameToken.Nonce : Array.Empty<byte>();
			var createdArray = wsUsernameToken.Created != null ? UTF8.GetBytes(wsUsernameToken.Created) : Array.Empty<byte>();
			var passwordArray = _password != null ? UTF8.GetBytes(_password) : Array.Empty<byte>();
			var hashArray = new byte[nonceArray.Length + createdArray.Length + passwordArray.Length];
			Array.Copy(nonceArray, 0, hashArray, 0, nonceArray.Length);
			Array.Copy(createdArray, 0, hashArray, nonceArray.Length, createdArray.Length);
			Array.Copy(passwordArray, 0, hashArray, nonceArray.Length + createdArray.Length, passwordArray.Length);

			var hash = SHA1.Create().ComputeHash(hashArray);
			var serverPasswordDigest = ToBase64String(hash);

			var clientPasswordDigest = wsUsernameToken.Password?.Value;
			return serverPasswordDigest == clientPasswordDigest;
		}
	}
}
