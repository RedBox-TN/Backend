using RedBox.Settings;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services.Configure<DatabaseSettings>(
	builder.Configuration.GetSection("MongoDB"));

var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
if (redisHost == null) 
	Environment.Exit(-1);

builder.Services.AddSingleton<IConnectionMultiplexer>(
	ConnectionMultiplexer.Connect(redisHost));

builder.Services.AddGrpc();

var app = builder.Build();

app.Run();