using RedBox.Services;
using RedBox.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("RedBoxDB"));

builder.AddRedBoxAuthenticationAndAuthorization();

builder.Services.AddGrpc();

var app = builder.Build();

app.UseRedBoxAuthenticationAndAuthorization();

app.MapGrpcService<DummyService>();

app.Run();