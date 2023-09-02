using Keychain.Settings;
using RedBox.Email_utility;
using RedBox.PermissionUtility;
using RedBox.Services;
using RedBox.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpcReflection();

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("RedBoxDB"));
var settings = builder.Configuration.GetSection("RedBoxSettings");
builder.Services.Configure<RedBoxSettings>(settings);
builder.Services.Configure<RedBoxEmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<IRedBoxEmailUtility, RedBoxEmailUtility>();
builder.Services.AddSingleton<IPermissionUtility, PermissionUtility>();

builder.AddRedBoxAuthenticationAndAuthorization();

builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize =
        (settings.GetValue<int>("MaxAttachmentSizeMb") * settings.GetValue<int>("MaxAttachmentsPerMsg") +
         settings.GetValue<int>("MaxMessageSizeMb")) * 1024 * 1024;
});

var app = builder.Build();

app.MapGrpcService<UserService>();
app.MapGrpcService<AdminService>();
app.MapGrpcService<RoleService>();

app.UseRedBoxAuthenticationAndAuthorization();

if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.Run();