using System;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml.Serialization;
using SoapCore.Extensibility;
using static System.Convert;
using static System.Text.Encoding;

namespace SoapCore
{
	public class WsMessageFilter : IAsyncMessageFilter
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

		public Task OnRequestExecuting(Message message)
		{
			WsUsernameToken wsUsernameToken;
			try
			{
				wsUsernameToken = GetWsUsernameToken(message);
				ValidateWsUsernameTokenModel(wsUsernameToken);
			}
			catch (Exception)
			{
				throw new AuthenticationException(_authMissingErrorMessage);
			}

			try
			{
				ValidateWsUsernameToken(wsUsernameToken);
			}
			catch (Exception)
			{
				throw new InvalidCredentialException(_authInvalidErrorMessage);
			}

			return Task.CompletedTask;
		}

		public Task OnResponseExecuting(Message message)
		{
			return Task.CompletedTask;
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

			return wsUsernameToken;
		}

		private bool IsPasswordClearText(WsUsernameToken.PasswordString password) =>
			string.IsNullOrEmpty(password?.Type) || _passwordTextType.Equals(password?.Type);

		private void ValidateWsUsernameTokenModel(WsUsernameToken wsUsernameToken)
		{
			if (!IsPasswordClearText(wsUsernameToken.Password))
			{
				if (!string.IsNullOrEmpty(wsUsernameToken.Nonce) ^ !string.IsNullOrEmpty(wsUsernameToken.Created))
				{
					throw new Exception();
				}

				if (!string.IsNullOrEmpty(wsUsernameToken.Nonce))
				{
					FromBase64String(wsUsernameToken.Nonce);
				}
			}
		}

		private void ValidateWsUsernameToken(WsUsernameToken wsUsernameToken)
		{
			if (wsUsernameToken.Username != _username)
			{
				throw new Exception();
			}

			if (IsPasswordClearText(wsUsernameToken.Password))
			{
				if (wsUsernameToken.Password?.Value == _password)
				{
					return;
				}

				throw new Exception();
			}

			var nonceArray = !string.IsNullOrEmpty(wsUsernameToken.Nonce) ? FromBase64String(wsUsernameToken.Nonce) : Array.Empty<byte>();
			var createdArray = !string.IsNullOrEmpty(wsUsernameToken.Created) ? UTF8.GetBytes(wsUsernameToken.Created) : Array.Empty<byte>();
			var passwordArray = !string.IsNullOrEmpty(_password) ? UTF8.GetBytes(_password) : Array.Empty<byte>();
			var hashArray = new byte[nonceArray.Length + createdArray.Length + passwordArray.Length];
			Buffer.BlockCopy(nonceArray, 0, hashArray, 0, nonceArray.Length);
			Buffer.BlockCopy(createdArray, 0, hashArray, nonceArray.Length, createdArray.Length);
			Buffer.BlockCopy(passwordArray, 0, hashArray, nonceArray.Length + createdArray.Length, passwordArray.Length);

			var hash = SHA1.Create().ComputeHash(hashArray);
			var serverPasswordDigest = ToBase64String(hash);

			var clientPasswordDigest = wsUsernameToken.Password?.Value;
			if (serverPasswordDigest != clientPasswordDigest)
			{
				throw new Exception();
			}
		}
	}
}
