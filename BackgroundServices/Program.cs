using BackgroundServices;
using StackExchange.Redis;

var host = Host.CreateApplicationBuilder(args);

host.Logging.ClearProviders();
host.Logging.AddConsole();

host.Services.Configure<Config>(host.Configuration.GetSection("Configurations"));

var redisHost = host.Configuration.GetSection("Configurations").GetSection("RedisConnectionString").Value;
if (redisHost == null) Environment.Exit(-1);

ConnectionMultiplexer? redis = null;

try
{
    redis = ConnectionMultiplexer.Connect(redisHost);
}
catch (Exception e)
{
    Console.Error.WriteLine($"Unable to connect to redis server {redisHost}");
    Console.Error.WriteLine(e.Message);
    Environment.Exit(-1);
}


host.Services.AddSingleton<IConnectionMultiplexer>(redis);
host.Services.AddHostedService<Worker>();

host.Build().Run();