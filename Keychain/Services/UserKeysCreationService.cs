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
public class UserKeysCreationService : GrpcUserKeysCreationServices.GrpcUserKeysCreationServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public UserKeysCreationService(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	public override async Task<Result> CreateUserMasterKey(MasterKey request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);

		var key = new Key
		{
			Data = request.EncryptedData.ToByteArray(),
			Iv = request.Iv.ToByteArray(),
			UserOwnerId = id
		};


		try
		{
			await keysCollection.InsertOneAsync(key);
		}
		catch (MongoWriteException e)
		{
			return new Result
			{
				Error = e.WriteError.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	public override async Task<Result> CreateUserKeys(UserKeyPairCreationRequest request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var privateKeys = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);
		var publicKeys = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);

		try
		{
			await privateKeys.InsertOneAsync(new Key
			{
				UserOwnerId = id,
				Data = request.EncryptedPrivateKey.ToByteArray(),
				Iv = request.Iv.ToByteArray()
			});
			await publicKeys.InsertOneAsync(new Key
			{
				UserOwnerId = id,
				Data = request.PublicKey.ToByteArray()
			});
		}
		catch (MongoWriteException e)
		{
			return new Result
			{
				Error = e.WriteError.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	[PermissionsRequired(DefaultPermissions.CreateGroups)]
	public override async Task<Result> CreateGroupKeys(GroupKeysCreationRequest request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);
		var supervisedGroupsCollection = _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection);

		try
		{
			await supervisedGroupsCollection.InsertOneAsync(new ChatKey
			{
				ChatCollectionName = request.GroupCollectionName,
				Data = request.EncryptedKeyForSupervisors.ToByteArray(),
				IsEncryptedWithUserPublicKey = true
			});

			await keysCollection.InsertOneAsync(new ChatKey
			{
				UserOwnerId = id,
				Data = request.EncryptedCreatorKey.ToByteArray(),
				Iv = request.Iv.ToByteArray(),
				ChatCollectionName = request.GroupCollectionName
			});

			await keysCollection.InsertManyAsync(request.MembersKeys.Select(memberKey => new ChatKey
			{
				UserOwnerId = memberKey.UserId, Data = memberKey.Data.ToByteArray(),
				ChatCollectionName = request.GroupCollectionName, IsEncryptedWithUserPublicKey = true
			}));
		}
		catch (MongoWriteException e)
		{
			return new Result
			{
				Error = e.WriteError.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	public override async Task<Result> CreateUserGroupKey(UserGroupKeyCreationRequest request,
		ServerCallContext context)
	{
		var keysCollection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);

		try
		{
			await keysCollection.InsertOneAsync(new ChatKey
			{
				UserOwnerId = request.UserId,
				ChatCollectionName = request.ChatCollectionName,
				Data = request.EncryptedKey.ToByteArray(),
				IsEncryptedWithUserPublicKey = true
			});
		}
		catch (MongoWriteException e)
		{
			return new Result
			{
				Error = e.WriteError.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	[PermissionsRequired(DefaultPermissions.CreateChats)]
	public override async Task<Result> CreateChatKeys(ChatKeyCreationRequest request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);
		var keySupervisorsCollection = _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection);

		try
		{
			await keysCollection.InsertManyAsync(new[]
			{
				new ChatKey
				{
					UserOwnerId = id,
					ChatCollectionName = request.ChatCollectionName,
					Data = request.EncryptedKey.ToByteArray(),
					Iv = request.Iv.ToByteArray()
				},
				new ChatKey
				{
					UserOwnerId = request.OtherUserId,
					ChatCollectionName = request.ChatCollectionName,
					Data = request.EncryptedKeyForOtherUser.ToByteArray(),
					IsEncryptedWithUserPublicKey = true
				}
			});
			await keySupervisorsCollection.InsertOneAsync(new ChatKey
			{
				ChatCollectionName = request.ChatCollectionName,
				Data = request.EncryptedKeyForSupervisors.ToByteArray(),
				IsEncryptedWithUserPublicKey = true
			});
		}
		catch (MongoWriteException e)
		{
			return new Result
			{
				Error = e.WriteError.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}
}