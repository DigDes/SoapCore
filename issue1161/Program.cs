using System.ServiceModel.Channels;
using SoapCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSoapCore();
builder.Services.AddSingleton<IMySoapService, MySoapService>();

var app = builder.Build();


app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.UseSoapEndpoint<IMySoapService>("/myService", new SoapEncoderOptions()
        {
            MessageVersion = MessageVersion.Soap11
        },
        SoapSerializer.XmlSerializer);
});

app.Run();
