using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using keychain;
using Keychain.Models;
using MongoDB.Driver;
using RedBoxAuth;
using Shared;

namespace Keychain.Services;

public partial class KeychainServices
{
	public override async Task<KeyResponse> GetUserMasterKey(Empty request, ServerCallContext context)
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

	public override async Task<KeyResponse> GetUserPublicKey(StringMessage request, ServerCallContext context)
	{
		var keysCollection = _database.GetCollection<Key>(_settings.UsersPublicKeysCollection);
		var key = await keysCollection.Find(k => k.UserOwnerId == request.Value).FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data)
		};
	}

	public override async Task<KeyResponse> GetUserPrivateKey(Empty request, ServerCallContext context)
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

	public override async Task<KeyResponse> GetChatKey(StringMessage request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<ChatKey>(_settings.ChatsKeysCollection);
		var key = await keysCollection.Find(k =>
				k.UserOwnerId == id && k.ChatCollectionName == request.Value && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeyResponse> GetGroupKey(StringMessage request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var keysCollection = _database.GetCollection<ChatKey>(_settings.GroupsKeysCollection);


		var key = await keysCollection.Find(k =>
				k.UserOwnerId == id && k.ChatCollectionName == request.Value && k.IsEncryptedWithUserPublicKey == null)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeysResponse> GetChatsKeys(Empty request, ServerCallContext context)
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

	public override async Task<KeysResponse> GetGroupsKey(Empty request, ServerCallContext context)
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
		Empty request, ServerCallContext context)
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
		Empty request, ServerCallContext context)
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