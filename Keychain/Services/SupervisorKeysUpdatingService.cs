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

[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
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

	public override async Task<Result> UpdateUserSupervisorMasterKey(MasterKey request,
		ServerCallContext context)
	{
		try
		{
			var id = context.GetUser().Id;
			var collection = _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection);

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

	public override async Task<Result> UpdateSupervisedChatKey(UpdateKeyRequest request, ServerCallContext context)
	{
		try
		{
			var chatKeys = _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection);

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

	public override async Task<Result> UpdateSupervisedGroupKey(UpdateKeyRequest request, ServerCallContext context)
	{
		try
		{
			var groupKeys = _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection);

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

	public override async Task<Result> UpdateMultipleSupervisorKeys(UpdateSupervisorKeysRequest request,
		ServerCallContext context)
	{
		try
		{
			if (request.MasterKey is not null)
			{
				var id = context.GetUser().Id;
				var collection = _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection);

				var update = Builders<Key>.Update
					.Set(k => k.Data, request.MasterKey.EncryptedData.ToByteArray())
					.Set(k => k.Iv, request.MasterKey.Iv.ToByteArray());


				await collection.UpdateOneAsync(k => k.UserOwnerId == id, update);
			}

			if (request.ChatKeys is not null)
			{
				var chatKeysCollection = _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection);

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
				var groupKeysCollection = _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection);

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