using Keychain.Services;
using Keychain.Settings;
using RedBoxAuth;
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

RequiredAuthServices.Add(builder.Services);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<KeychainService>();

app.Run();