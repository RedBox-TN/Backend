using Grpc.Core;
using keychain;
using Keychain.Models;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace Keychain.Services;

public partial class KeychainServices
{
	public override async Task<Result> CreateUserMasterKey(MasterKey request, ServerCallContext context)
	{
		if (request.EncryptedData.IsEmpty || request.Iv.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Error = e.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	public override async Task<Result> CreateUserKeyPair(UserKeyPairCreationRequest request, ServerCallContext context)
	{
		if (request.EncryptedPrivateKey.IsEmpty || request.Iv.IsEmpty || request.PublicKey.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Error = e.Message,
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
		if (string.IsNullOrEmpty(request.GroupCollectionName) || request.EncryptedKeyForSupervisors.IsEmpty ||
		    request.EncryptedCreatorKey.IsEmpty || request.Iv.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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

			var a = request.MembersKeys;
			await keysCollection.InsertManyAsync(request.MembersKeys.Select(memberKey => new ChatKey
			{
				UserOwnerId = memberKey.UserId, Data = memberKey.Data.ToByteArray(),
				ChatCollectionName = request.GroupCollectionName, IsEncryptedWithUserPublicKey = true
			}));
		}
		catch (Exception e)
		{
			return new Result
			{
				Error = e.Message,
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
		if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.ChatCollectionName) ||
		    request.EncryptedKey.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Error = e.Message,
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
		if (string.IsNullOrEmpty(request.ChatCollectionName) || string.IsNullOrEmpty(request.OtherUserId) ||
		    request.EncryptedKey.IsEmpty || request.Iv.IsEmpty || request.EncryptedKeyForOtherUser.IsEmpty ||
		    request.EncryptedKeyForSupervisors.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Error = e.Message,
				Status = Status.Error
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}
}