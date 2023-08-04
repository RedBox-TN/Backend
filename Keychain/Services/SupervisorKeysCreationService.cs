using Grpc.Core;
using keychain;
using Keychain.Models;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using Shared.Models;
using Status = keychain.Status;

namespace Keychain.Services;

[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
public class SupervisorKeysCreationService : GrpcSupervisorKeysCreationServices.GrpcSupervisorKeysCreationServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public SupervisorKeysCreationService(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	public override async Task<Result> CreateSupervisorUserMasterKey(SupervisorKeyCreationRequest request,
		ServerCallContext context)
	{
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

	public override async Task<Result> CreateSupervisorKeyPair(UserKeyPairCreationRequest request,
		ServerCallContext context)
	{
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