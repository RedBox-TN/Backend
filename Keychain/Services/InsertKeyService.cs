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

public class InsertKeyService : GrpcInsertKeyServices.GrpcInsertKeyServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public InsertKeyService(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	[AuthenticationRequired]
	public override Task<Result> InsertUserKeys(InsertUserKeysRequest request, ServerCallContext context)
	{
		var privateKeys = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);
		var publicKeys = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);

		var user = context.GetHttpContext().Items[Constants.UserContextKey] as User;

		try
		{
			privateKeys.InsertOneAsync(new Key
			{
				UserOwnerId = user!.Id,
				Data = request.PrivateKey.ToByteArray()
			});

			publicKeys.InsertOneAsync(new Key
			{
				UserOwnerId = user.Id,
				Data = request.PublicKey.ToByteArray()
			});
		}
		catch (Exception)
		{
			return Task.FromResult(new Result
			{
				Status = Status.Error
			});
		}

		return Task.FromResult(new Result
		{
			Status = Status.Ok
		});
	}

	[RequiredPermissions(DefaultPermissions.CreateGroups)]
	public override Task<Result> InsertGroupKeys(InsertGroupKeysRequest request, ServerCallContext context)
	{
		var privateKeys = _database.GetCollection<Key>(_settings.GroupsPrivateKeyCollection);
		var publicKeys = _database.GetCollection<Key>(_settings.GroupsPublicKeysCollection);

		var user = context.GetHttpContext().Items[Constants.UserContextKey] as User;

		try
		{
			privateKeys.InsertOneAsync(new GroupKey
			{
				UserOwnerId = user!.Id,
				Data = request.CreatorPrivateKey.ToByteArray(),
				GroupCollectionName = request.GroupId,
				IsEncryptedWithUserKey = false
			});

			publicKeys.InsertOneAsync(new GroupKey
			{
				Data = request.GroupPublicKey.ToByteArray(),
				GroupCollectionName = request.GroupId
			});


			var membersKey = from k in request.MembersPrivate
				select new GroupKey
				{
					UserOwnerId = k.UserId,
					Data = k.Data.ToByteArray(),
					GroupCollectionName = request.GroupId,
					IsEncryptedWithUserKey = true
				};

			privateKeys.InsertManyAsync(membersKey);
		}
		catch (Exception)
		{
			return Task.FromResult(new Result
			{
				Status = Status.Error
			});
		}

		return Task.FromResult(new Result
		{
			Status = Status.Ok
		});
	}

	[RequiredPermissions(DefaultPermissions.Administrator)]
	public override Task<Result> InsertSupervisorKeys(InsertSupervisorKeysRequest request, ServerCallContext context)
	{
		var privateKeys = _database.GetCollection<Key>(_settings.SupervisorsPrivateKeysCollection);
		var publicKeys = _database.GetCollection<Key>(_settings.SupervisorsPublicKeysCollection);

		var user = context.GetHttpContext().Items[Constants.UserContextKey] as User;

		try
		{
			privateKeys.InsertOneAsync(new Key
			{
				UserOwnerId = user!.Id,
				Data = request.PrivateKey.ToByteArray(),
				IsEncryptedWithUserKey = false
			});

			publicKeys.InsertOneAsync(new Key
			{
				Data = request.PublicKey.ToByteArray(),
			});
		}
		catch (Exception)
		{
			return Task.FromResult(new Result
			{
				Status = Status.Error
			});
		}

		return Task.FromResult(new Result
		{
			Status = Status.Ok
		});
	}

	[RequiredPermissions(DefaultPermissions.CreateGroups)]
	public override Task<Result> InsertUserGroupKey(InsertUserAdditionalKeyRequest request, ServerCallContext context)
	{
		var privateKeys = _database.GetCollection<Key>(_settings.GroupsPrivateKeyCollection);

		try
		{
			privateKeys.InsertOneAsync(new GroupKey
			{
				UserOwnerId = request.Private.UserId,
				Data = request.Private.Data.ToByteArray(),
				GroupCollectionName = request.ContextId,
				IsEncryptedWithUserKey = true
			});
		}
		catch (Exception)
		{
			return Task.FromResult(new Result
			{
				Status = Status.Error
			});
		}

		return Task.FromResult(new Result
		{
			Status = Status.Ok
		});
	}

	[RequiredPermissions(DefaultPermissions.ReadOthersChat)]
	public override Task<Result> InsertUserSupervisorKey(InsertUserAdditionalKeyRequest request,
		ServerCallContext context)
	{
		var privateKeys = _database.GetCollection<Key>(_settings.SupervisorsPrivateKeysCollection);

		try
		{
			privateKeys.InsertOneAsync(new Key
			{
				UserOwnerId = request.Private.UserId,
				Data = request.Private.Data.ToByteArray(),
				IsEncryptedWithUserKey = true
			});
		}
		catch (Exception)
		{
			return Task.FromResult(new Result
			{
				Status = Status.Error
			});
		}

		return Task.FromResult(new Result
		{
			Status = Status.Ok
		});
	}
}