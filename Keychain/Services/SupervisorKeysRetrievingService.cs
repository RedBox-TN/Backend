using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using keychain;
using Keychain.Models;
using MongoDB.Driver;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using Shared;
using Shared.Models;

namespace Keychain.Services;

public partial class KeychainServices
{
	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeyResponse> GetUserSupervisorMasterKey(Empty request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var key = await _database.GetCollection<Key>(_settings.SupervisorsMasterKeysCollection)
			.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == null).FirstOrDefaultAsync();

		return new KeyResponse
		{
			Id = key.Id,
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	public override async Task<KeyResponse> GetSupervisorPublicKey(Empty request, ServerCallContext context)
	{
		var key = await _database.GetCollection<Key>(_settings.SupervisorPublicKeyCollection).Find(_ => true)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Id = key.Id,
			Data = ByteString.CopyFrom(key.Data)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeyResponse> GetSupervisorPrivateKey(Empty request, ServerCallContext context)
	{
		var key = await _database.GetCollection<Key>(_settings.SupervisorPrivateKeyCollection).Find(_ => true)
			.FirstOrDefaultAsync();

		return new KeyResponse
		{
			Id = key.Id,
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeyResponse> GetSupervisedChatKey(StringMessage request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var key = await _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection)
			.Find(k => k.UserOwnerId == id && k.ChatCollectionName == request.Value).FirstOrDefaultAsync();

		return new KeyResponse
		{
			Id = key.Id,
			ChatCollectionName = key.ChatCollectionName,
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeyResponse> GetSupervisedGroupKey(StringMessage request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var key = await _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection)
			.Find(k => k.UserOwnerId == id && k.ChatCollectionName == request.Value).FirstOrDefaultAsync();

		return new KeyResponse
		{
			Id = key.Id,
			ChatCollectionName = key.ChatCollectionName,
			Data = ByteString.CopyFrom(key.Data),
			Iv = ByteString.CopyFrom(key.Iv)
		};
	}

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeysResponse> GetSupervisedChatsKeys(Empty request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.SupervisedChatsKeysCollection)
			.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == null).ToListAsync();

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

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeysResponse> GetSupervisedGroupsKeys(Empty request, ServerCallContext context)
	{
		var id = context.GetUser().Id;
		var foundKeys = await _database.GetCollection<ChatKey>(_settings.SupervisedGroupsKeysCollection)
			.Find(k => k.UserOwnerId == id && k.IsEncryptedWithUserPublicKey == null).ToListAsync();

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

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeyEncryptedWithPublicKey> GetUserSupervisorMasterKeyEncryptedWithPublicKey(
		Empty request,
		ServerCallContext context)
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

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeysEncryptedWithPublicKey> GetSupervisedChatsKeysEncryptedWithPublicKey(Empty request,
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

	[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
	public override async Task<KeysEncryptedWithPublicKey> GetSupervisedGroupsKeysEncryptedWithPublicKey(Empty request,
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