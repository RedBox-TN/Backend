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
	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<Result> UpdateUserSupervisorMasterKey(MasterKey request,
		ServerCallContext context)
	{
		if (request.EncryptedData.IsEmpty || request.Iv.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<Result> UpdateSupervisedChatKey(UpdateKeyRequest request, ServerCallContext context)
	{
		if (request.KeyData.IsEmpty || request.Iv.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<Result> UpdateSupervisedGroupKey(UpdateKeyRequest request, ServerCallContext context)
	{
		if (request.KeyData.IsEmpty || request.Iv.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

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
		catch (Exception e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<Result> UpdateMultipleSupervisorKeys(UpdateSupervisorKeysRequest request,
		ServerCallContext context)
	{
		try
		{
			if (!request.MasterKey.EncryptedData.IsEmpty && !request.MasterKey.Iv.IsEmpty)
			{
				var id = context.GetUser().Id;
				var collection = _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection);

				var update = Builders<Key>.Update
					.Set(k => k.Data, request.MasterKey.EncryptedData.ToByteArray())
					.Set(k => k.Iv, request.MasterKey.Iv.ToByteArray());


				await collection.UpdateOneAsync(k => k.UserOwnerId == id, update);
			}

			if (request.ChatKeys.Count > 0)
			{
				var chatKeysCollection = _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection);

				foreach (var key in request.ChatKeys)
				{
					if (string.IsNullOrEmpty(key.KeyId) || key.KeyData.IsEmpty || key.Iv.IsEmpty) continue;

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
		catch (Exception e)
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