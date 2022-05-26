using System;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Convert;
using static System.Text.Encoding;

namespace SoapCore.Tests.MessageFilter
{
	[TestClass]
	public class WsMessageFilterTests
	{
		private const string _passwordDigest = @"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest";
		private const string _passwordText = @"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

		private static readonly XNamespace _soapenv11 = "http://schemas.xmlsoap.org/soap/envelope/";
		private static readonly XNamespace _wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
		private static readonly XNamespace _wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

		[TestMethod]
		[ExpectedException(typeof(InvalidCredentialException))]
		public async Task IncorrectCredentialsNotAuthrorized()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "INVALID_USERNAME"),
				new XElement(_wsse + "Password", "INAVLID_PASSWORD"));

			var filter = new WsMessageFilter("yourusername", "yourpassword");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		public async Task PasswordIsOptional()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"));

			var filter = new WsMessageFilter("yourusername", null);
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		public async Task PasswordTypeTextIsComparedAsIs()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(_wsse + "Password", new XAttribute("Type", _passwordText), "yourpassword"));

			var filter = new WsMessageFilter("yourusername", "yourpassword");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		public async Task PasswordInDigestIsDecoded()
		{
			var clearTextPassword = "yourpassword";
			var passwordDigest = ToBase64String(SHA1.Create().ComputeHash(UTF8.GetBytes(clearTextPassword)));
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(
					_wsse + "Password",
					new XAttribute("Type", _passwordDigest),
					new XText(passwordDigest)));

			var filter = new WsMessageFilter("yourusername", clearTextPassword);
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		[ExpectedException(typeof(AuthenticationException))]
		public async Task NonceCantBePresentWithoutCreated()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(_wsse + "Password", new XAttribute("Type", _passwordDigest), "yourpassword"),
				new XElement(_wsse + "Nonce", ToBase64String(Guid.NewGuid().ToByteArray())));

			var filter = new WsMessageFilter("yourusername", "yourpassword");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		[ExpectedException(typeof(AuthenticationException))]
		public async Task CreatedCantBePresentWithoutNonce()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(_wsse + "Password", new XAttribute("Type", _passwordDigest), "yourpassword"),
				new XElement(_wsu + "Created", "2003-07-16T01:24:32Z"));

			var filter = new WsMessageFilter("yourusername", "yourpassword");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		public async Task UseNonceAndCreatedInDigestAgainstReplayAttack()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(_wsse + "Password", new XAttribute("Type", _passwordDigest), "U1GjAqli//AHdFxRZUbVeJYz6GA="),
				new XElement(_wsse + "Nonce", "l//4xNUs0LzslTkEA/Ch1Q=="),
				new XElement(_wsu + "Created", "2020-03-06T19:58:28.134Z"));

			var filter = new WsMessageFilter("yourusername", "Password");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidCredentialException))]
		public async Task IncorrectPasswordNotAuthorizedAgainstDigest()
		{
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(_wsse + "Password", new XAttribute("Type", _passwordDigest), "U1GjAqli//AHdFxRZUbVeJYz6GA="),
				new XElement(_wsse + "Nonce", "l//4xNUs0LzslTkEA/Ch1Q=="),
				new XElement(_wsu + "Created", "2020-03-06T19:58:28.134Z"));

			var filter = new WsMessageFilter("yourusername", "IncorrectPassword");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		[TestMethod]
		[ExpectedException(typeof(AuthenticationException))]
		public async Task InvalidNonceIsNotAuthorizedEvenInCleartext()
		{
			var notBase64Encoded = "!@#$%^&*()_+";
			var usernameToken = new XElement(
				_wsse + "UsernameToken",
				new XElement(_wsse + "Username", "yourusername"),
				new XElement(_wsse + "Password", new XAttribute("Type", _passwordDigest), "yourpassword"),
				new XElement(_wsse + "Nonce", notBase64Encoded),
				new XElement(_wsu + "Created", "2020-03-06T19:58:28.134Z"));

			var filter = new WsMessageFilter("yourusername", "yourpassword");
			await filter.OnRequestExecuting(CreateMessage(usernameToken));
		}

		private static Message CreateMessage(XNode usernameToken)
		{
			var envelope = new XElement(
				_soapenv11 + "Envelope",
				new XAttribute(XNamespace.Xmlns + "wsse", _wsse.NamespaceName),
				new XAttribute(XNamespace.Xmlns + "soap", _soapenv11.NamespaceName),
				new XElement(
					_soapenv11 + "Header",
					new XElement(
						_wsse + "Security",
						usernameToken)),
				new XElement(
					_soapenv11 + "Body",
					new XElement(
						XName.Get("Ping", "http://tempuri.org/"),
						"abc")));

			var doc = new XDocument(envelope);
			return Message.CreateMessage(doc.CreateReader(), int.MaxValue, MessageVersion.Soap11);
		}
	}
}
