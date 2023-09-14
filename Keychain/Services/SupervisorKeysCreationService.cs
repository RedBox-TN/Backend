using Grpc.Core;
using keychain;
using Keychain.Models;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace Keychain.Services;

public partial class KeychainServices
{
	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<Result> CreateSupervisorUserMasterKey(SupervisorKeyCreationRequest request,
		ServerCallContext context)
	{
		if (request.EncryptedKey.IsEmpty || string.IsNullOrEmpty(request.UserId))
			return new Result
			{
				Status = Status.MissingParameters
			};

		var keysCollection = _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection);

		try
		{
			await keysCollection.InsertOneAsync(new Key
			{
				UserOwnerId = request.UserId,
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

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<Result> CreateSupervisorKeyPair(UserKeyPairCreationRequest request,
		ServerCallContext context)
	{
		if (request.EncryptedPrivateKey.IsEmpty || request.Iv.IsEmpty || request.PublicKey.IsEmpty)
			return new Result
			{
				Status = Status.MissingParameters
			};

		var privateKeys = _database.GetCollection<Key>(_settings.SupervisorPrivateKeyCollection);
		var publicKeys = _database.GetCollection<Key>(_settings.SupervisorPublicKeyCollection);

		try
		{
			await privateKeys.InsertOneAsync(new Key
			{
				Data = request.EncryptedPrivateKey.ToByteArray(),
				Iv = request.Iv.ToByteArray()
			});
			await publicKeys.InsertOneAsync(new Key
			{
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
}