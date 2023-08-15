using Grpc.Core;
using keychain;
using Keychain.Models;
using MongoDB.Driver;
using RedBoxAuth;
using Shared;
using Status = Shared.Status;

namespace Keychain.Services;

public partial class KeychainServices
{
	public override async Task<Result> UpdateUserMasterKey(MasterKey request, ServerCallContext context)
	{
		try
		{
			var id = context.GetUser().Id;
			var collection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);

			var update = Builders<Key>.Update
				.Set(k => k.Data, request.EncryptedData.ToByteArray())
				.Set(k => k.Iv, request.Iv.ToByteArray());

			await collection.UpdateOneAsync(k => k.UserOwnerId == id, update);

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

	public override async Task<Result> UpdateUserKeyPair(UpdateUserKeyPairRequest request, ServerCallContext context)
	{
		try
		{
			var id = context.GetUser().Id;

			var privateKeys = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);

			var privateKeyUpdate = Builders<Key>.Update
				.Set(k => k.Data, request.PrivateKeyData.ToByteArray())
				.Set(k => k.Iv, request.PrivateKeyIv.ToByteArray());

			await privateKeys.UpdateOneAsync(k => k.UserOwnerId == id, privateKeyUpdate);

			if (request.HasPublicKeyData)
			{
				var publicKeys = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);

				var publicKeyUpdate = Builders<Key>.Update
					.Set(k => k.Data, request.PublicKeyData.ToByteArray());

				await publicKeys.UpdateOneAsync(k => k.UserOwnerId == id, publicKeyUpdate);
			}
		}
		catch (MongoException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	public override async Task<Result> UpdateUserChatKey(UpdateKeyRequest request, ServerCallContext context)
	{
		try
		{
			var chatKeys = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);

			var keyUpdate = Builders<ChatKey>.Update
				.Set(k => k.Data, request.KeyData.ToByteArray())
				.Set(k => k.Iv, request.Iv.ToByteArray());

			keyUpdate = request.IsEncryptedWithPublicKey
				? keyUpdate.Set(k => k.IsEncryptedWithUserPublicKey, true)
				: keyUpdate.Unset(k => k.IsEncryptedWithUserPublicKey);

			await chatKeys.UpdateOneAsync(k => k.Id == request.KeyId, keyUpdate);

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

	public override async Task<Result> UpdateUserGroupKey(UpdateKeyRequest request, ServerCallContext context)
	{
		try
		{
			var groupKeys = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);

			var keyUpdate = Builders<ChatKey>.Update
				.Set(k => k.Data, request.KeyData.ToByteArray())
				.Set(k => k.Iv, request.Iv.ToByteArray());

			keyUpdate = request.IsEncryptedWithPublicKey
				? keyUpdate.Set(k => k.IsEncryptedWithUserPublicKey, true)
				: keyUpdate.Unset(k => k.IsEncryptedWithUserPublicKey);

			await groupKeys.UpdateOneAsync(k => k.Id == request.KeyId, keyUpdate);

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

	public override async Task<Result> UpdateMultipleUserKeys(UpdateKeysRequest request, ServerCallContext context)
	{
		try
		{
			var id = context.GetUser().Id;
			if (request.MasterKey is not null)
			{
				var collection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);

				var update = Builders<Key>.Update
					.Set(k => k.Data, request.MasterKey.EncryptedData.ToByteArray())
					.Set(k => k.Iv, request.MasterKey.Iv.ToByteArray());


				await collection.UpdateOneAsync(k => k.UserOwnerId == id, update);
			}

			if (request.KeyPair is not null)
			{
				var privateKeysCollection = _database.GetCollection<ChatKey>(_settings.UsersPrivateKeysCollection);

				var update = Builders<ChatKey>.Update
					.Set(k => k.Data, request.KeyPair.PrivateKeyData.ToByteArray())
					.Set(k => k.Iv, request.KeyPair.PrivateKeyIv.ToByteArray());

				await privateKeysCollection.UpdateOneAsync(k => k.UserOwnerId == id, update);

				if (request.KeyPair.HasPublicKeyData)
				{
					var publicKeysCollection = _database.GetCollection<ChatKey>(_settings.UsersPublicKeysCollection);

					update = Builders<ChatKey>.Update
						.Set(k => k.Data, request.KeyPair.PublicKeyData.ToByteArray());

					await publicKeysCollection.UpdateOneAsync(k => k.UserOwnerId == id, update);
				}
			}

			if (request.ChatKeys is not null)
			{
				var chatKeysCollection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);

				foreach (var key in request.ChatKeys)
				{
					var update = Builders<ChatKey>.Update
						.Set(k => k.Data, key.KeyData.ToByteArray())
						.Set(k => k.Iv, key.Iv.ToByteArray());

					update = key.IsEncryptedWithPublicKey
						? update.Set(k => k.IsEncryptedWithUserPublicKey, true)
						: update.Unset(k => k.IsEncryptedWithUserPublicKey);

					await chatKeysCollection.UpdateOneAsync(k => k.Id == key.KeyId, update);
				}
			}

			if (request.GroupKeys is not null)
			{
				var groupKeysCollection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);

				foreach (var key in request.GroupKeys)
				{
					var update = Builders<ChatKey>.Update
						.Set(k => k.Data, key.KeyData.ToByteArray())
						.Set(k => k.Iv, key.Iv.ToByteArray());

					update = key.IsEncryptedWithPublicKey
						? update.Set(k => k.IsEncryptedWithUserPublicKey, true)
						: update.Unset(k => k.IsEncryptedWithUserPublicKey);

					await groupKeysCollection.UpdateOneAsync(k => k.Id == key.KeyId, update);
				}
			}
		}
		catch (MongoException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}
}