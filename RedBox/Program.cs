using RedBox.Email_utility;
using RedBox.Services;
using RedBox.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpcReflection();

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("RedBoxDB"));
builder.Services.Configure<RedBoxSettings>(builder.Configuration.GetSection("RedBoxSettings"));
builder.Services.Configure<RedBoxEmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<IRedBoxEmailUtility, RedBoxEmailUtility>();

builder.AddRedBoxAuthenticationAndAuthorization();

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<UserService>();
app.UseRedBoxAuthenticationAndAuthorization();

if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.Run();