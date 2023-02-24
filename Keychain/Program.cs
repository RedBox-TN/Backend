using Keychain.Services;
using Keychain.Settings;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("MongoDB"));

var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
if (redisHost == null)
	Environment.Exit(-1);

var redis = ConnectionMultiplexer.Connect(redisHost);

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddSingleton<ISessionEncryptionSettings>(
	new SessionEncryptionSettings(redis));

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<KeychainService>();

app.Run();