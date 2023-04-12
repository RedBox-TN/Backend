using Grpc.Core;
using Keychain.Models;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Keychain.Services;

public class KeychainService : GrpcKeyService.GrpcKeyServiceBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _databaseSettings;
	private readonly IDatabase _redis;

	public KeychainService(IConnectionMultiplexer redis, IOptions<DatabaseSettings> dbSettings)
	{
		_redis = redis.GetDatabase();
		_databaseSettings = dbSettings.Value;

		var client = new MongoClient(dbSettings.Value.ConnectionString);
		_database = client.GetDatabase(dbSettings.Value.DatabaseName);
	}

	public override Task<KeyResponse> Create(CreateRequest request, ServerCallContext context)
	{
		switch (request.Type)
		{
			case KeyType.UserPrivate:
				return InsertUserPrivate(request);
			case KeyType.UserPublic:
				return InsertUserPublic(request);
			case KeyType.GroupPrivate:
				return InsertGroupPrivate(request);
			case KeyType.GroupPublic:
				return InsertGroupPublic(request);
			case KeyType.ExecutivePrivate:
				return InsertExecutivePrivate(request);
			case KeyType.ExecutivePublic:
				return InsertExecutivePublic(request);
			default:
				_logger.LogWarning("wrong KeyType in create request: {S}", request.ToString());
				return Task.FromResult(new KeyResponse
				{
					Status = Status.Error
				});
		}
	}

	private Task<KeyResponse> InsertUserPrivate(CreateRequest request)
	{
		var collection = _database.GetCollection<Key>(_databaseSettings.UsersPrivateKeysCollection);

		var key = new Key
		{
			Data = request.Data.ToByteArray(),
			UserId = userId
		};

		collection.InsertOne(key);

		return Task.FromResult(new KeyResponse
		{
			Status = Status.Ok,
			Id = key.Id
		});
	}

	private Task<KeyResponse> InsertUserPublic(CreateRequest request)
	{
		var collection = _database.GetCollection<Key>(_databaseSettings.UsersPublicKeysCollection);

		var key = new Key
		{
			Data = request.Data.ToByteArray(),
			UserId = _sessionEncryptionSettings.DecryptToken(request.Token)
		};

		collection.InsertOneAsync(key);

		return Task.FromResult(new KeyResponse
		{
			Status = Status.Ok,
			Id = key.Id
		});
	}

	private Task<KeyResponse> InsertGroupPrivate(CreateRequest request)
	{
		var collection = _database.GetCollection<Key>(_databaseSettings.GroupsPrivateKeyCollection);

		var key = new GroupKey
		{
			Data = request.Data.ToByteArray(),
			UserId = _sessionEncryptionSettings.DecryptToken(request.Token),
			GroupId = request.GroupId
		};

		collection.InsertOneAsync(key);

		return Task.FromResult(new KeyResponse
		{
			Status = Status.Ok,
			Id = key.Id
		});
	}

	private Task<KeyResponse> InsertGroupPublic(CreateRequest request)
	{
		var collection = _database.GetCollection<Key>(_databaseSettings.GroupsPublicKeysCollection);

		var key = new GroupKey
		{
			Data = request.Data.ToByteArray(),
			UserId = _sessionEncryptionSettings.DecryptToken(request.Token),
			GroupId = request.GroupId
		};

		collection.InsertOneAsync(key);

		return Task.FromResult(new KeyResponse
		{
			Status = Status.Ok,
			Id = key.Id
		});
	}

	private Task<KeyResponse> InsertExecutivePrivate(CreateRequest request)
	{
		var collection = _database.GetCollection<Key>(_databaseSettings.ExecutivesPrivateKeysCollection);

		var key = new ExecutiveKey
		{
			Data = request.Data.ToByteArray(),
			UserId = _sessionEncryptionSettings.DecryptToken(request.Token),
			ChatId = request.ChatId
		};

		collection.InsertOneAsync(key);

		return Task.FromResult(new KeyResponse
		{
			Status = Status.Ok,
			Id = key.Id
		});
	}

	private Task<KeyResponse> InsertExecutivePublic(CreateRequest request)
	{
		var collection = _database.GetCollection<Key>(_databaseSettings.ExecutivesPublicKeysCollection);

		var key = new ExecutiveKey
		{
			Data = request.Data.ToByteArray(),
			UserId = _sessionEncryptionSettings.DecryptToken(request.Token),
			ChatId = request.ChatId
		};

		collection.InsertOneAsync(key);

		return Task.FromResult(new KeyResponse
		{
			Status = Status.Ok,
			Id = key.Id
		});
	}

	public override Task<KeyResponse> Get(GetRequest request, ServerCallContext context)
	{
		return base.Get(request, context);
	}

	public override Task<KeyResponse> Update(UpdateRequest request, ServerCallContext context)
	{
		return base.Update(request, context);
	}

	public override Task<Result> Delete(DeleteRequest request, ServerCallContext context)
	{
		return base.Delete(request, context);
	}
}