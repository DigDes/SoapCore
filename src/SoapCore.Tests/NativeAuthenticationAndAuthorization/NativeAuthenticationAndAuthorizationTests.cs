using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.NativeAuthenticationAndAuthorization
{
	[TestClass]
	public class NativeAuthenticationAndAuthorizationTests
	{
		[ClassInitialize]
#pragma warning disable IDE0060 // Remove unused parameter
		public static void StartServer(TestContext context)
#pragma warning restore IDE0060 // Remove unused parameter
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:5054")
					.UseStartup<Startup>()
					.Build();
				host.Run();
			}).Wait(1000);
		}

		public ITestService CreateClient(string authorizationHeaderValue = null)
		{
			string address = string.Format("http://{0}:5054/Service.svc", "localhost");

			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(address));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);

			if (!string.IsNullOrWhiteSpace(authorizationHeaderValue))
			{
				channelFactory.Endpoint.EndpointBehaviors.Add(new AuthorizationHeaderEndpointBehavior(authorizationHeaderValue));
			}

			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void CheckNoAuthenticationProvidedAndNoneRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient();
			var result = client.JwtAuthenticationAndAuthorizationIActionResultUnprotected(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckAuthenticationProvidedAndNoneRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role2"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationIActionResultUnprotected(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckNoAuthenticationProvidedAndAuthenticationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			try
			{
				var client = CreateClient();
				var result = client.JwtAuthenticationAndAuthorizationIActionResultJustAuthenticated(inputModel);
			}
			catch (System.ServiceModel.Security.MessageSecurityException ex)
			{
				Assert.IsTrue(ex.Message.Contains("request is unauthorized"));
			}
		}

		[TestMethod]
		public void CheckAuthenticationProvidedAndAuthenticationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role2"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationIActionResultJustAuthenticated(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckNoAuthenticationProvidedAndRoleAuthorizationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			try
			{
				var client = CreateClient();
				var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			}
			catch (System.ServiceModel.Security.MessageSecurityException ex)
			{
				Assert.IsTrue(ex.Message.Contains("request is unauthorized"));
			}
		}

		[TestMethod]
		public void CheckWrongRoleProvidedAndRoleAuthorizationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			try
			{
				var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
				{
					new Claim(ClaimTypes.Name, "user1"),
					new Claim(ClaimTypes.Role, "role2"),
				}));
				var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			}
			catch (System.ServiceModel.Security.MessageSecurityException ex)
			{
				Console.WriteLine(ex);
				Assert.IsTrue(ex.Message.Contains("request was forbidden"));
			}
		}

		[TestMethod]
		public void CheckRightRoleProvidedAndRoleAuthorizationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role1"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckNoAuthenticationProvidedAndPolicyAuthorizationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			try
			{
				var client = CreateClient();
				var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			}
			catch (System.ServiceModel.Security.MessageSecurityException ex)
			{
				Assert.IsTrue(ex.Message.Contains("request is unauthorized"));
			}
		}

		[TestMethod]
		public void CheckWrongClaimsProvidedAndPolicyAuthorizationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			try
			{
				var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
				{
					new Claim(ClaimTypes.Name, "user1"),
					new Claim(ClaimTypes.Role, "role2"),
				}));
				var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			}
			catch (System.ServiceModel.Security.MessageSecurityException ex)
			{
				Console.WriteLine(ex);
				Assert.IsTrue(ex.Message.Contains("request was forbidden"));
			}
		}

		[TestMethod]
		public void CheckRightClaimsProvidedAndPolicyAuthorizationRequired()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role1"),
				new Claim("someclaim", "somevalue"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckBadRequest()
		{
			ComplexModelInput inputModel = null;

			try
			{
				var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
				{
					new Claim(ClaimTypes.Name, "user1"),
					new Claim(ClaimTypes.Role, "role1"),
				}));
				var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			}
			catch (ProtocolException ex)
			{
				Console.WriteLine(ex);
				Assert.IsTrue(ex.Message.Contains("(400)"));
			}
		}

		[TestMethod]
		public void CheckInternalServerError()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 123,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role1"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationIActionResult(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is odd.");
		}

		[TestMethod]
		public void CheckActionResultReturnType()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role1"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationActionResult(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckGenericActionResultReturnType()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role1"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationGenericActionResult(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result, "Number is even.");
		}

		[TestMethod]
		public void CheckComplexGenericActionResultReturnType()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 124,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient("Bearer " + GenerateToken(new List<Claim>
			{
				new Claim(ClaimTypes.Name, "user1"),
				new Claim(ClaimTypes.Role, "role1"),
			}));
			var result = client.JwtAuthenticationAndAuthorizationComplexGenericActionResult(inputModel);
			Assert.IsNotNull(result);
			Assert.AreEqual(result.StringProperty, "Number is even.");
		}

		private string GenerateToken(List<Claim> claims)
		{
			var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes("12345678900987654321123456789009")), SecurityAlgorithms.HmacSha256);
			var token = new JwtSecurityToken(
			   claims: claims,
			   expires: DateTime.UtcNow.AddHours(1),
			   signingCredentials: signingCredentials);
			var tokenHandler = new JwtSecurityTokenHandler();
			var encryptedToken = tokenHandler.WriteToken(token);
			return encryptedToken;
		}
	}
}
