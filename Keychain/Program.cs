using Keychain.Services;
using Keychain.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("MongoDB"));

await builder.AddRedBoxBasicAuthorizationAsync();

builder.Services.AddGrpc();

var app = builder.Build();

app.UseRedBoxBasicAuthorization();

app.MapGrpcService<KeychainServices>();

app.Run();