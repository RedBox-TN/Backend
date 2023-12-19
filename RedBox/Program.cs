using Microsoft.Extensions.Diagnostics.HealthChecks;
using RedBox.Email_utility;
using RedBox.Permission_utility;
using RedBox.Permission_Utility;
using RedBox.Providers;
using RedBox.Services;
using RedBox.Settings;
using RedBoxAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RedBoxDatabaseSettings>(builder.Configuration.GetSection("RedBoxDB"));
builder.Services.Configure<RedBoxApplicationSettings>(builder.Configuration.GetSection("RedBoxApplicationSettings"));
builder.Services.Configure<RedBoxEmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<IRedBoxEmailUtility, RedBoxEmailUtility>();
builder.Services.AddSingleton<IPermissionUtility, PermissionUtility>();
builder.Services.AddSingleton<IClientsRegistryProvider, ClientsRegistryProvider>();

await builder.AddRedBoxAuthenticationAndAuthorizationAsync();

var appSettings = builder.Configuration.GetSection("RedBoxApplicationSettings").Get<RedBoxApplicationSettings>() ??
                  new RedBoxApplicationSettings();

builder.Services.AddGrpc(options =>
{
	options.MaxReceiveMessageSize =
		(appSettings.MaxAttachmentSizeMb * (appSettings.MaxAttachmentsPerMsg + 1) + appSettings.MaxMessageSizeMb) *
		1024 * 1024;
});

builder.Services.AddGrpcHealthChecks().AddCheck<RedBoxGrpcHealthCheck>("Backend");

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
	options.Delay = TimeSpan.FromSeconds(appSettings.GrpcHealthCheckStartupDelay);
	options.Period = TimeSpan.FromSeconds(appSettings.GrpcHealthCheckInterval);
});

builder.Services.AddCors(o => o.AddPolicy("AllowAll", b =>
{
	b.AllowAnyOrigin()
		.AllowAnyMethod()
		.AllowAnyHeader()
		.WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding", "X-Grpc-Web",
			"User-Agent");
}));


var app = builder.Build();

app.UseGrpcWeb();
app.UseCors();

app.MapGrpcHealthChecksService().EnableGrpcWeb().RequireCors("AllowAll");

app.MapGrpcService<AccountServices>().EnableGrpcWeb().RequireCors("AllowAll");
app.MapGrpcService<AdminService>().EnableGrpcWeb().RequireCors("AllowAll");
app.MapGrpcService<ConversationService>().EnableGrpcWeb().RequireCors("AllowAll");
app.MapGrpcService<SupervisedConversationService>().EnableGrpcWeb().RequireCors("AllowAll");

app.UseRedBoxAuthenticationAndAuthorization();

app.Run();