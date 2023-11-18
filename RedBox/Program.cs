using Microsoft.Extensions.Diagnostics.HealthChecks;
using RedBox.Email_utility;
using RedBox.Permission_Utility;
using RedBox.PermissionUtility;
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

builder.AddRedBoxAuthenticationAndAuthorization();

var appSettings = builder.Configuration.GetSection("RedBoxApplicationSettings").Get<RedBoxApplicationSettings>() ??
                  new RedBoxApplicationSettings();

builder.Services.AddGrpc(options =>
{
	options.MaxReceiveMessageSize =
		(appSettings.MaxAttachmentSizeMb * (appSettings.MaxAttachmentsPerMsg + 1) + appSettings.MaxMessageSizeMb) *
		1024 * 1024;
});

builder.Services.AddGrpcHealthChecks().AddCheck<RedBoxGrpcHealthCheck>("Backend up and running");

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
	options.Delay = TimeSpan.FromSeconds(appSettings.GrpcHealthCheckStartupDelay);
	options.Period = TimeSpan.FromSeconds(appSettings.GrpcHealthCheckInterval);
});

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowBlazorAppOrigin",
		b => b
			.AllowAnyOrigin()
			.AllowAnyHeader()
			.AllowAnyMethod()
			.WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding", "X-Grpc-Web",
				"User-Agent"));
	;
});


var app = builder.Build();

app.UseCors("AllowBlazorAppOrigin");

app.UseGrpcWeb();

app.MapGrpcHealthChecksService();

app.MapGrpcService<AccountServices>().EnableGrpcWeb();
app.MapGrpcService<AdminService>().EnableGrpcWeb();
app.MapGrpcService<ConversationService>().EnableGrpcWeb();
app.MapGrpcService<SupervisedConversationService>().EnableGrpcWeb();

app.UseRedBoxAuthenticationAndAuthorization();

app.Run();