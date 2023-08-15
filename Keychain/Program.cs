using Keychain.Services;
using Keychain.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpcReflection();

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("MongoDB"));

builder.AddRedBoxBasicAuthorization();

builder.Services.AddGrpc();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.UseRedBoxBasicAuthorization();

app.MapGrpcService<KeychainServices>();

app.Run();