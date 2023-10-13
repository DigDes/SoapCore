using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.ServiceModel.Channels;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SoapCore.Extensibility;
using SoapCore.Tests.Model;

namespace SoapCore.Tests
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSoapCore();
			services.TryAddSingleton<TestService>();
			services.AddSoapModelBindingFilter(new ModelBindingFilter.TestModelBindingFilter(new List<Type> { typeof(ComplexModelInputForModelBindingFilter) }));
			services.AddScoped<ActionFilter.TestActionFilter>();
			services.AddMvc();
			services
				.AddAuthentication(authentication =>
				{
					authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
					authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
				})
				.AddJwtBearer(bearer =>
				{
					bearer.RequireHttpsMetadata = false;
					bearer.SaveToken = true;
					bearer.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuerSigningKey = true,
						IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("12345678900987654321123456789009")),
						ValidateIssuer = false,
						ValidateAudience = false,
						RoleClaimType = ClaimTypes.Role,
						ClockSkew = TimeSpan.Zero
					};
				});

			services.AddSoapMessageProcessor(new AuthorizeOperationMessageProcessor(new Dictionary<string, Type>
			{
				{ "/Service.svc".ToLowerInvariant(), typeof(TestService) },
				{ "/ServiceCI.svc".ToLowerInvariant(), typeof(TestService) },
				{ "/Service.asmx".ToLowerInvariant(), typeof(TestService) },
				{ "/WSA10Service.svc".ToLowerInvariant(), typeof(TestService) },
				{ "/WSA11ISO88591Service.svc".ToLowerInvariant(), typeof(TestService) },
				{ "/ServiceWithDifferentEncodings.asmx".ToLowerInvariant(), typeof(TestService) },
				{ "/ServiceWithOverwrittenContentType.asmx".ToLowerInvariant(), typeof(TestService) },
			}));
			services.AddAuthorization(options =>
			{
				options.AddPolicy("something", policy => policy.RequireClaim("someclaim", "somevalue"));
			});
		}

#if !NETCOREAPP3_0_OR_GREATER
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseAuthentication();

			//app.UseMiddleware<RequestResponseLoggingMiddleware>();
			app.UseWhen(ctx => ctx.Request.Headers.ContainsKey("SOAPAction"), app2 =>
			{
				app2.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Headers.ContainsKey("SOAPAction"), app2 =>
			{
				// For case insensitive path test
				app2.UseSoapEndpoint<TestService>("/ServiceCI.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer, caseInsensitivePath: true);
			});

			app.UseWhen(ctx => !ctx.Request.Headers.ContainsKey("SOAPAction"), app2 =>
			{
				app.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 }, SoapSerializer.DataContractSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("asmx"), app2 =>
			{
				app2.UseSoapEndpoint<TestService>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("/WSA10Service.svc"), app2 =>
			{
				var transportBinding = new HttpTransportBindingElement();
				var textEncodingBinding = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, System.Text.Encoding.UTF8);

				app.UseSoapEndpoint<TestService>("/WSA10Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 }, SoapSerializer.DataContractSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("/WSA11ISO88591Service.svc"), app2 =>
			{
				var soapEncodingOptions = new SoapEncoderOptions
				{
					MessageVersion = MessageVersion.Soap11,
					WriteEncoding = Encoding.GetEncoding("ISO-8859-1")
				};

				app.UseSoapEndpoint<TestService>("/WSA11ISO88591Service.svc", soapEncodingOptions, SoapSerializer.DataContractSerializer);
			});

			app.UseMvc();

		}
#else
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();
			app.UseAuthentication();

			//app.UseMiddleware<RequestResponseLoggingMiddleware>();
			app.UseWhen(ctx => ctx.Request.Headers.ContainsKey("SOAPAction") || ctx.Request.ContentType.StartsWith("multipart"), app2 =>
			{
				app2.UseRouting();

				app2.UseEndpoints(x =>
				{
					x.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				});
			});

			app.UseWhen(ctx => ctx.Request.Headers.ContainsKey("SOAPAction"), app2 =>
			{
				app2.UseRouting();

				app2.UseEndpoints(x =>
				{
					x.UseSoapEndpoint<TestService>("/ServiceCI.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer, caseInsensitivePath: true);
				});
			});

			app.UseWhen(ctx => !ctx.Request.Headers.ContainsKey("SOAPAction") && !ctx.Request.ContentType.StartsWith("multipart"), app2 =>
			{
				app2.UseRouting();

				app2.UseEndpoints(x =>
				{
					x.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 }, SoapSerializer.DataContractSerializer);
				});
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("asmx"), app2 =>
			{
				app2.UseRouting();

				app2.UseSoapEndpoint<TestService>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("/WSA10Service.svc"), app2 =>
			{
				app2.UseRouting();

				app.UseSoapEndpoint<TestService>("/WSA10Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 }, SoapSerializer.DataContractSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("/WSA11ISO88591Service.svc"), app2 =>
			{
				var soapEncodingOptions = new SoapEncoderOptions
				{
					MessageVersion = MessageVersion.Soap11,
					WriteEncoding = Encoding.GetEncoding("ISO-8859-1")
				};

				app.UseSoapEndpoint<TestService>("/WSA11ISO88591Service.svc", soapEncodingOptions, SoapSerializer.DataContractSerializer);
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("asmx"), app2 =>
			{
				app2.UseRouting();

				var soapEncodingOptions = new SoapEncoderOptions
				{
					MessageVersion = MessageVersion.Soap11,
					WriteEncoding = Encoding.UTF8,
					OverwriteResponseContentType = false
				};

				app2.UseSoapEndpoint<TestService>(opt =>
				{
					opt.Path = "/ServiceWithDifferentEncodings.asmx";
					opt.EncoderOptions = new[] { soapEncodingOptions };
					opt.OmitXmlDeclaration = false;
					opt.SoapSerializer = SoapSerializer.XmlSerializer;
				});
			});

			app.UseWhen(ctx => ctx.Request.Path.Value.Contains("asmx"), app2 =>
			{
				app2.UseRouting();

				var soapEncodingOptions = new SoapEncoderOptions
				{
					MessageVersion = MessageVersion.Soap11,
					WriteEncoding = Encoding.UTF8,
					OverwriteResponseContentType = true
				};

				app2.UseSoapEndpoint<TestService>("/ServiceWithOverwrittenContentType.asmx", soapEncodingOptions, SoapSerializer.XmlSerializer);
			});
		}
#endif
	}
}
