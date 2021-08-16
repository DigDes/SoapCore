using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SoapCore.Benchmark
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<PingService>();
		}

		public void Configure(IApplicationBuilder app)
		{
			app.UseSoapEndpoint<PingService>("/TestService.asmx", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			app.Use(async (ctx, next) =>
			{
				await ctx.Response.WriteAsync("").ConfigureAwait(false);
			});
		}
	}
}
