using ExpiredKeyRemover;
using StackExchange.Redis;

var host = Host.CreateApplicationBuilder(args);
host.Services.Configure<Config>(host.Configuration.GetSection("Configurations"));

var redisHost = host.Configuration.GetSection("Configurations").GetSection("RedisConnectionString").Value;
if (redisHost == null)
	Environment.Exit(-1);

var redis = ConnectionMultiplexer.Connect(redisHost);

host.Services.AddSingleton<IConnectionMultiplexer>(redis);
host.Services.AddHostedService<Worker>();

host.Build().Run();