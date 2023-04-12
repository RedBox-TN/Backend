using RedBox.Settings;
using RedBoxAuth;
using RedBoxAuth.Settings;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("RedBoxDB"));

builder.Services.Configure<AuthDatabaseSettings>(
	builder.Configuration.GetSection("UsersDB"));

builder.Services.Configure<AuthenticationOptions>(
	builder.Configuration.GetSection("AuthenticationOptions"));

var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
if (redisHost == null)
	Environment.Exit(-1);

var redis = ConnectionMultiplexer.Connect(redisHost);

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddRedBoxAuth();

builder.Services.AddGrpc();

var app = builder.Build();

app.UseRedBoxAuth();

app.Run();