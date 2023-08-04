using Grpc.Core;
using keychain;
using Keychain.Models;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using Status = keychain.Status;

namespace Keychain.Services;

[AuthenticationRequired]
public class UserKeysUpdatingService : GrpcUserKeysUpdatingServices.GrpcUserKeysUpdatingServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public UserKeysUpdatingService(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	public override async Task<Result> UpdateUserMasterKey(UpdateKeyRequest request, ServerCallContext context)
	{
		try
		{
			var collection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);

			var update = Builders<Key>.Update
				.Set(k => k.Data, request.KeyData.ToByteArray())
				.Set(k => k.Iv, request.Iv.ToByteArray());

			update = request.IsEncryptedWithPublicKey
				? update.Set(k => k.IsEncryptedWithUserPublicKey, true)
				: update.Unset(k => k.IsEncryptedWithUserPublicKey);


			await collection.UpdateOneAsync(k => k.Id == request.KeyId, update);

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
			var privateKeys = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);
			var publicKeys = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);

			var privateKeyUpdate = Builders<Key>.Update
				.Set(k => k.Data, request.PrivateKeyData.ToByteArray())
				.Set(k => k.Iv, request.PrivateKeyIv.ToByteArray());

			await privateKeys.UpdateOneAsync(k => k.Id == request.PrivateKeyId, privateKeyUpdate);

			var publicKeyUpdate = Builders<Key>.Update
				.Set(k => k.Data, request.PublicKeyData.ToByteArray());

			await publicKeys.UpdateOneAsync(k => k.Id == request.PublicKeyId, publicKeyUpdate);

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
			if (request.SupervisorKey is not null)
			{
				var supervisorKeysCollection =
					_database.GetCollection<ChatKey>(_settings.SupervisorsMasterKeysCollection);

				var update = Builders<ChatKey>.Update
					.Set(k => k.Data, request.SupervisorKey.KeyData.ToByteArray())
					.Set(k => k.Iv, request.SupervisorKey.Iv.ToByteArray());

				update = request.SupervisorKey.IsEncryptedWithPublicKey
					? update.Set(k => k.IsEncryptedWithUserPublicKey, true)
					: update.Unset(k => k.IsEncryptedWithUserPublicKey);

				await supervisorKeysCollection.UpdateOneAsync(k => k.Id == request.SupervisorKey.KeyId, update);
			}

			if (request.PrivateKey is not null)
			{
				var privateKeysCollection = _database.GetCollection<ChatKey>(_settings.UsersPrivateKeysCollection);

				var update = Builders<ChatKey>.Update
					.Set(k => k.Data, request.PrivateKey.KeyData.ToByteArray())
					.Set(k => k.Iv, request.PrivateKey.Iv.ToByteArray());

				update = request.PrivateKey.IsEncryptedWithPublicKey
					? update.Set(k => k.IsEncryptedWithUserPublicKey, true)
					: update.Unset(k => k.IsEncryptedWithUserPublicKey);

				await privateKeysCollection.UpdateOneAsync(k => k.Id == request.PrivateKey.KeyId, update);
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