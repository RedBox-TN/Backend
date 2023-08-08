using Grpc.Core;
using keychain;
using Keychain.Models;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using Shared.Models;
using Status = keychain.Status;

namespace Keychain.Services;

[AuthenticationRequired]
public class SupervisorKeysDeletionService : GrpcSupervisorKeysDeletionServices.GrpcSupervisorKeysDeletionServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public SupervisorKeysDeletionService(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	public override async Task<Result> DeleteUserSupervisorMasterKey(DeleteKeyFromUserIdRequest request,
		ServerCallContext context)
	{
		var user = context.GetUser();
		var collection = _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection);

		try
		{
			if (request.HasUserId &&
			    AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts))
				await collection.DeleteOneAsync(k => k.UserOwnerId == request.UserId);
			else
				await collection.DeleteOneAsync(k => k.UserOwnerId == user.Id);

			return new Result
			{
				Status = Status.Ok
			};
		}
		catch (MongoException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}
	}

	[PermissionsRequired(DefaultPermissions.DeleteSupervisedChat)]
	public override async Task<Result> DeleteSupervisorChatKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var collection = _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection);

		try
		{
			await collection.DeleteOneAsync(k => k.Id == request.Id);

			return new Result
			{
				Status = Status.Ok
			};
		}
		catch (MongoException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}
	}

	[PermissionsRequired(DefaultPermissions.DeleteSupervisedChat)]
	public override async Task<Result> DeleteSupervisorGroupKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var collection = _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection);

		try
		{
			await collection.DeleteOneAsync(k => k.Id == request.Id);

			return new Result
			{
				Status = Status.Ok
			};
		}
		catch (MongoException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}
	}
}