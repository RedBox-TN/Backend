using Grpc.Core;
using keychain;
using Keychain.Models;
using MongoDB.Driver;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace Keychain.Services;

public partial class KeychainServices
{
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
	public override async Task<Result> DeleteSupervisorChatKey(StringMessage request, ServerCallContext context)
	{
		if (string.IsNullOrEmpty(request.Value))
			return new Result
			{
				Status = Status.MissingParameters
			};

		var collection = _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection);

		try
		{
			await collection.DeleteOneAsync(k => k.Id == request.Value);

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
	public override async Task<Result> DeleteSupervisorGroupKey(StringMessage request, ServerCallContext context)
	{
		if (string.IsNullOrEmpty(request.Value))
			return new Result
			{
				Status = Status.MissingParameters
			};

		var collection = _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection);

		try
		{
			await collection.DeleteOneAsync(k => k.Id == request.Value);

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