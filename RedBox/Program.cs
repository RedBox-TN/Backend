using RedBox.Email_utility;
using RedBox.Encryption_utility;
using RedBox.Services;
using RedBox.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpcReflection();

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("RedBoxDB"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<RedBoxSettings>(builder.Configuration.GetSection("RedBox"));

builder.Services.AddSingleton<IEncryptionUtility, EncryptionUtility>();
builder.Services.AddSingleton<IEmailUtility, EmailUtility>();

builder.AddRedBoxAuthenticationAndAuthorization();

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<UserService>();
app.UseRedBoxAuthenticationAndAuthorization();

if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.Run();