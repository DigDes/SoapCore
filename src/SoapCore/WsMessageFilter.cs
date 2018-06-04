using System;
using System.Security.Authentication;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;

namespace SoapCore {
    class WsMessageFilter : IMessageFilter {

		private static string _username;
		private static string _password;

		public WsMessageFilter(string username, string password) {
			_username = username;
			_password = password;
		}

		public void OnRequestExecuting(Message message) {
			WsUsernameToken wsUsernameToken = null;

			try {
				wsUsernameToken = GetWsUsernameToken(message);
			}
			catch (Exception) {
				throw new AuthenticationException("Referenced security token could not be retrieved");
			}

			if (wsUsernameToken == null)
				throw new AuthenticationException("Referenced security token could not be retrieved");

			if (!ValidateWsUsernameToken(wsUsernameToken))
				throw new InvalidCredentialException("Authentication error: Authentication failed: the supplied credential is not right");
		}

		public void OnResponseExecuting(Message message) { }

		private WsUsernameToken GetWsUsernameToken(Message message) {
			WsUsernameToken wsUsernameToken = null;
			for (var i = 0; i < message.Headers.Count; i++) {
				if (message.Headers[i].Name.ToLower() == "security") {
					var reader = message.Headers.GetReaderAtHeader(i);
					reader.Read();
					DataContractSerializer serializer = new DataContractSerializer(typeof(WsUsernameToken));
					wsUsernameToken = (WsUsernameToken)serializer.ReadObject(reader, true);
					reader.Close();
				}
			}

			return wsUsernameToken;
		}

		private bool ValidateWsUsernameToken(WsUsernameToken wsUsernameToken) {
			return wsUsernameToken.Username == _username && wsUsernameToken.Password == _password;
		}

	}
}
