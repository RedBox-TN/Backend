using Google.Protobuf;
using Grpc.Core;
using keychain;
using Keychain.Models;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth;
using RedBoxAuth.Authorization;

namespace Keychain.Services;

[AuthenticationRequired]
public sealed class UserKeysRetrievingServices : GrpcUserKeysRetrievingServices.GrpcUserKeysRetrievingServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly DatabaseSettings _settings;

	public UserKeysRetrievingServices(IOptions<DatabaseSettings> options)
	{
		_settings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
	}

	public override async Task<KeyResponse> GetUserMasterKey(Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<Key>(_settings.UsersMasterKeysCollection);
		var key = await keysCollection.Find(k => k.UserOwnerId == id).FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeyResponse> GetUserPublicKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var keysCollection = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);
		var key = await keysCollection.Find(k => k.UserOwnerId == request.Id).FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data)
		};
	}

	public override async Task<KeyResponse> GetUserPrivateKey(Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<Key>(_settings.UsersPrivateKeysCollection);

		var key = await keysCollection.Find(k => k.UserOwnerId == id).FirstOrDefaultAsync();
		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeyResponse> GetChatKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);
		var key = await keysCollection.Find(k =>
				k.UserOwnerId == id && k.ChatCollectionName == request.Id && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeyResponse> GetGroupKey(KeyFromIdRequest request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);


		var key = await keysCollection.Find(k =>
				k.UserOwnerId == id && k.ChatCollectionName == request.Id && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeysResponse> GetChatsKeys(Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection)
			.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == null).ToListAsync();

		if (foundKeys.Count == 0) return new KeysResponse();

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

	public override async Task<KeysResponse> GetGroupsKey(Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection)
			.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == null).ToListAsync();

		if (foundKeys.Count == 0) return new KeysResponse();

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

	public override async Task<KeysEncryptedWithPublicKey> GetUserChatKeysEncryptedWithPublicKey(
		Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var collection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);

		var foundKeys = await collection.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == true)
			.ToListAsync();

		if (foundKeys.Count == 0)
			return new KeysEncryptedWithPublicKey();

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

	public override async Task<KeysEncryptedWithPublicKey> GetUserGroupKeysEncryptedWithPublicKey(
		Nil request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var collection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);

		var foundKeys = await collection.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == true)
			.ToListAsync();

		if (foundKeys.Count == 0)
			return new KeysEncryptedWithPublicKey();

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