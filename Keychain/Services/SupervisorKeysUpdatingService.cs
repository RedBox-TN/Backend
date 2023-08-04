using Grpc.Core;
using keychain;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Keychain.Services;

public class SupervisorKeysUpdatingService : GrpcSupervisorKeysUpdatingServices.GrpcSupervisorKeysUpdatingServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public SupervisorKeysUpdatingService(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	public override async Task<Result> UpdateUserSupervisorMasterKey(UpdateKeyRequest request,
		ServerCallContext context)
	{
		return await base.UpdateUserSupervisorMasterKey(request, context);
	}

	public override async Task<Result> UpdateSupervisedChatKey(UpdateKeyRequest request, ServerCallContext context)
	{
		return await base.UpdateSupervisedChatKey(request, context);
	}

	public override async Task<Result> UpdateSupervisedGroupKey(UpdateKeyRequest request, ServerCallContext context)
	{
		return await base.UpdateSupervisedGroupKey(request, context);
	}

	public override async Task<Result> UpdateMultipleSupervisorKeys(UpdateKeysRequest request,
		ServerCallContext context)
	{
		return await base.UpdateMultipleSupervisorKeys(request, context);
	}
}