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
	public override async Task<Result> DeleteUserMasterKey(DeleteKeyFromUserIdRequest request,
		ServerCallContext context)
	{
		var user = context.GetUser();
		var collection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);

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

	public override async Task<Result> DeleteUserKeyPair(DeleteKeyFromUserIdRequest request, ServerCallContext context)
	{
		var user = context.GetUser();
		var privateKeysCollection = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);
		var publicKeysCollection = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);

		try
		{
			if (request.HasUserId &&
			    AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts))
			{
				var filter = Builders<Key>.Filter.Eq(k => k.UserOwnerId, request.UserId);
				await privateKeysCollection.DeleteOneAsync(filter);
				await publicKeysCollection.DeleteOneAsync(filter);
			}
			else
			{
				var filter = Builders<Key>.Filter.Eq(k => k.UserOwnerId, user.Id);
				await privateKeysCollection.DeleteOneAsync(filter);
				await publicKeysCollection.DeleteOneAsync(filter);
			}

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

	public override async Task<Result> DeleteUserChatKey(StringMessage request, ServerCallContext context)
	{
		if (string.IsNullOrEmpty(request.Value))
			return new Result
			{
				Status = Status.MissingParameters
			};

		var id = context.GetUser().Id;
		var collection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);

		try
		{
			await collection.DeleteOneAsync(k => k.UserOwnerId == id && k.ChatCollectionName == request.Value);
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

	public override async Task<Result> DeleteUserGroupKey(StringMessage request, ServerCallContext context)
	{
		if (string.IsNullOrEmpty(request.Value))
			return new Result
			{
				Status = Status.MissingParameters
			};

		var id = context.GetUser().Id;
		var collection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);

		try
		{
			await collection.DeleteOneAsync(k => k.UserOwnerId == id && k.ChatCollectionName == request.Value);
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

	public override async Task<Result> DeleteAllUserKeys(DeleteKeyFromUserIdRequest request, ServerCallContext context)
	{
		var user = context.GetUser();
		var masterKeysCollection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);
		var privateKeysCollection = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);
		var publicKeysCollection = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);
		var chatKeysCollection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);
		var groupKeysCollection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);

		try
		{
			if (request.HasUserId &&
			    AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts))
			{
				var userKeyFilter = Builders<Key>.Filter.Eq(k => k.UserOwnerId, request.UserId);
				await masterKeysCollection.DeleteOneAsync(userKeyFilter);
				await privateKeysCollection.DeleteOneAsync(userKeyFilter);
				await publicKeysCollection.DeleteOneAsync(userKeyFilter);

				var chatKeyFilter = Builders<ChatKey>.Filter.Eq(k => k.UserOwnerId, request.UserId);
				await chatKeysCollection.DeleteManyAsync(chatKeyFilter);
				await groupKeysCollection.DeleteOneAsync(chatKeyFilter);
			}
			else
			{
				var userKeyFilter = Builders<Key>.Filter.Eq(k => k.UserOwnerId, user.Id);
				await masterKeysCollection.DeleteOneAsync(userKeyFilter);
				await privateKeysCollection.DeleteOneAsync(userKeyFilter);
				await publicKeysCollection.DeleteOneAsync(userKeyFilter);

				var chatKeyFilter = Builders<ChatKey>.Filter.Eq(k => k.UserOwnerId, user.Id);
				await chatKeysCollection.DeleteManyAsync(chatKeyFilter);
				await groupKeysCollection.DeleteOneAsync(chatKeyFilter);
			}

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