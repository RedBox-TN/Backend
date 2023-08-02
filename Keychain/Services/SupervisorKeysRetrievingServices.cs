using Google.Protobuf;
using Grpc.Core;
using keychain;
using Keychain.Models;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using Shared.Models;

namespace Keychain.Services;

[AuthenticationRequired]
public sealed class
	SupervisorKeysRetrievingServices : GrpcSupervisorKeysRetrievingServices.GrpcSupervisorKeysRetrievingServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public SupervisorKeysRetrievingServices(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeyResponse> GetUserSupervisorMasterKey(Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection);
		var key = await keysCollection.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeyEncryptedWithPublicKey> GetUserSupervisorKeyEncryptedWithPublicKey(
		Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var key = await _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection)
			.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == true).FirstOrDefaultAsync();

		return new KeyEncryptedWithPublicKey
		{
			KeyId = key.Id,
			EncryptedKeyData = ByteString.CopyFrom(key.Data)
		};
	}

	public override async Task<KeyResponse> GetSupervisorPublicKey(Nil request, ServerCallContext context)
	{
		var key = await _database.GetCollection<Key>(_settings.SupervisorPublicKeyCollection)
			.Find(_ => true, new FindOptions { BatchSize = 1 }).FirstAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeyResponse> GetSupervisorPrivateKey(Nil request, ServerCallContext context)
	{
		var key = await _database.GetCollection<Key>(_settings.SupervisorPrivateKeyCollection)
			.Find(_ => true, new FindOptions { BatchSize = 1 }).FirstAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeyResponse> GetSupervisedChatKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var foundKey = await _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection)
			.Find(k => k.ChatCollectionName == request.Id && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			ChatCollectionName = foundKey.ChatCollectionName,
			Data = ByteString.CopyFrom(foundKey.Data),
			Iv = ByteString.CopyFrom(foundKey.Iv)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeysResponse> GetSupervisedChatsKeys(Nil request, ServerCallContext context)
	{
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection)
			.Find(k => k.IsEncryptedWithUserPublicKey == null).ToListAsync();

		var keys = new KeyResponse[foundKeys.Count];
		for (var i = 0; i < foundKeys.Count; i++)
			keys[i] = new KeyResponse
			{
				ChatCollectionName = foundKeys[i].ChatCollectionName,
				Data = ByteString.CopyFrom(foundKeys[i].Data),
				Iv = ByteString.CopyFrom(foundKeys[i].Iv)
			};

		return new KeysResponse
		{
			Keys = { keys }
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeyResponse> GetSupervisedGroupKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var foundKey = await _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection)
			.Find(k => k.ChatCollectionName == request.Id && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			ChatCollectionName = foundKey.ChatCollectionName,
			Data = ByteString.CopyFrom(foundKey.Data),
			Iv = ByteString.CopyFrom(foundKey.Iv)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeysResponse> GetSupervisedGroupsKeys(Nil request, ServerCallContext context)
	{
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection)
			.Find(k => k.IsEncryptedWithUserPublicKey == null).ToListAsync();

		var keys = new KeyResponse[foundKeys.Count];
		for (var i = 0; i < foundKeys.Count; i++)
			keys[i] = new KeyResponse
			{
				ChatCollectionName = foundKeys[i].ChatCollectionName,
				Data = ByteString.CopyFrom(foundKeys[i].Data),
				Iv = ByteString.CopyFrom(foundKeys[i].Iv)
			};

		return new KeysResponse
		{
			Keys = { keys }
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeysEncryptedWithPublicKey> GetSupervisedChatsKeysEncryptedWithPublicKey(Nil request,
		ServerCallContext context)
	{
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection)
			.Find(k => k.IsEncryptedWithUserPublicKey == true).ToListAsync();

		var keys = new KeyEncryptedWithPublicKey[foundKeys.Count];

		for (var i = 0; i < foundKeys.Count; i++)
			keys[i] = new KeyEncryptedWithPublicKey
			{
				KeyId = foundKeys[i].Id,
				EncryptedKeyData = ByteString.CopyFrom(foundKeys[i].Data)
			};

		return new KeysEncryptedWithPublicKey
		{
			Keys = { keys }
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOthersChat)]
	public override async Task<KeysEncryptedWithPublicKey> GetSupervisedGroupsKeysEncryptedWithPublicKey(Nil request,
		ServerCallContext context)
	{
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection)
			.Find(k => k.IsEncryptedWithUserPublicKey == true).ToListAsync();

		var keys = new KeyEncryptedWithPublicKey[foundKeys.Count];

		for (var i = 0; i < foundKeys.Count; i++)
			keys[i] = new KeyEncryptedWithPublicKey
			{
				KeyId = foundKeys[i].Id,
				EncryptedKeyData = ByteString.CopyFrom(foundKeys[i].Data)
			};

		return new KeysEncryptedWithPublicKey
		{
			Keys = { keys }
		};
	}
}