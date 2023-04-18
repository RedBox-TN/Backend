using Keychain.Services;
using Keychain.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("MongoDB"));

builder.AddUserRetrieval();

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<KeychainService>();

app.Run();