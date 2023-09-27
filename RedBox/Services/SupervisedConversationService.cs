using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RedBox.Models;
using RedBox.Settings;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

[PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
public class SupervisedConversationService : GrpcSupervisedConversationService.GrpcSupervisedConversationServiceBase
{
	private readonly RedBoxApplicationSettings _appSettings;
	private readonly RedBoxDatabaseSettings _dbSettings;
	private readonly IMongoClient _mongoClient;

	public SupervisedConversationService(IOptions<RedBoxDatabaseSettings> dbSettings,
		IOptions<RedBoxApplicationSettings> appSettings)
	{
		_dbSettings = dbSettings.Value;
		_appSettings = appSettings.Value;

		_mongoClient = new MongoClient(_dbSettings.ConnectionString);
	}

	public override async Task<ChatsResponse> GetAllChats(Empty request, ServerCallContext context)
	{
		var user = context.GetUser();
		var chats = await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
			.Find(Builders<Chat>.Filter.Not(Builders<Chat>.Filter.In(c => c.Id, user.ChatIds))).ToListAsync();

		var result = new GrpcChat[chats.Count];
		for (var i = 0; i < chats.Count; i++)
			result[i] = new GrpcChat
			{
				Id = chats[i].Id,
				CreatedAt = Timestamp.FromDateTime(chats[i].CreatedAt),
				Members = { chats[i].MembersIds },
				Messages = { await GetMessageFromCollectionAsync(chats[i].Id!) }
			};

		return new ChatsResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Chats = { result }
		};
	}

	public override async Task<GroupsResponse> GetAllGroups(Empty request, ServerCallContext context)
	{
		var ids = context.GetUser().GroupIds;
		if (ids.Length == 0)
			return new GroupsResponse
			{
				Result = new Result
				{
					Status = Status.Ok
				}
			};

		var found = await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection)
			.Find(Builders<Group>.Filter.Not(Builders<Group>.Filter.In(g => g.Id, ids)))
			.ToListAsync();

		var result = new GrpcGroup[found.Count];
		for (var i = 0; i < found.Count; i++)
			result[i] = new GrpcGroup
			{
				Id = found[i].Id,
				Name = found[i].Name,
				Admins = { found[i].AdminsIds },
				Members = { found[i].MembersIds },
				CreatedAt = Timestamp.FromDateTime(found[i].CreatedAt),
				Messages = { await GetMessageFromCollectionAsync(found[i].Id!, isGroup: true) }
			};

		return new GroupsResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Groups = { result }
		};
	}

	public override async Task<ChunkResponse> GetMessagesInRange(MessageChunkRequest request,
		ServerCallContext context)
	{
		List<Message> messages;
		if (request.Collection.HasChat)
			messages = await _mongoClient.GetDatabase(_dbSettings.ChatsDatabase)
				.GetCollection<Message>(request.Collection.Chat).Find(_ => true)
				.Skip(request.Chunk * _appSettings.MsgRetrieveChunkSize).Limit(_appSettings.MsgRetrieveChunkSize)
				.ToListAsync();
		else
			messages = await _mongoClient.GetDatabase(_dbSettings.GroupsDatabase)
				.GetCollection<Message>(request.Collection.Group).Find(_ => true)
				.Skip(request.Chunk * _appSettings.MsgRetrieveChunkSize).Limit(_appSettings.MsgRetrieveChunkSize)
				.ToListAsync();

		var result = new GrpcMessage[messages.Count];
		for (var i = 0; i < messages.Count; i++)
			result[i] = new GrpcMessage
			{
				Id = messages[i].Id,
				Timestamp = Timestamp.FromDateTime(messages[i].Timestamp),
				SenderId = messages[i].SenderId,
				EncryptedText = ByteString.CopyFrom(messages[i].EncryptedText),
				Iv = ByteString.CopyFrom(messages[i].Iv),
				Attachments =
				{
					ToGrpcAttachments(messages[i].Attachments)
				}
			};

		return new ChunkResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Messages =
			{
				result
			}
		};
	}

	public override async Task<GrpcAttachment> GetAttachmentData(AttachmentRequest request, ServerCallContext context)
	{
		var bucket = new GridFSBucket(_mongoClient.GetDatabase(_dbSettings.GridFsDatabase),
			new GridFSBucketOptions
			{
				BucketName = request.BucketName,
				ChunkSizeBytes = _dbSettings.GridFsChunkSizeBytes
			});

		var fileMetadata =
			await (await bucket.FindAsync(Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, new ObjectId(request.FileId))))
				.FirstAsync();

		return new GrpcAttachment
		{
			Id = request.FileId,
			Name = fileMetadata.Filename,
			Data = ByteString.CopyFrom(await bucket.DownloadAsBytesAsync(fileMetadata.Id, new GridFSDownloadOptions
			{
				CheckMD5 = false
			}))
		};
	}

	private static GrpcAttachment[] ToGrpcAttachments(Attachment[]? attachments)
	{
		if (attachments is null) return Array.Empty<GrpcAttachment>();

		var result = new GrpcAttachment[attachments.Length];
		for (var i = 0; i < attachments.Length; i++)
			result[i] = new GrpcAttachment
			{
				Id = attachments[i].Id,
				Name = attachments[i].Name
			};

		return result;
	}

	private async Task<GrpcMessage?> GetMessageFromCollectionAsync(string collectionId, string? messageId = null,
		bool isGroup = false)
	{
		var collection = isGroup
			? _mongoClient.GetDatabase(_dbSettings.GroupsDatabase).GetCollection<Message>(collectionId)
			: _mongoClient.GetDatabase(_dbSettings.ChatsDatabase).GetCollection<Message>(collectionId);

		Message? found;
		if (messageId is not null)
			found = await collection.Find(m => m.Id == messageId && !m.UserDeleted).FirstAsync();
		else
			found = await collection.Find(m => !m.UserDeleted).SortByDescending(m => m.Timestamp).FirstAsync();

		if (found is null) return null;

		return new GrpcMessage
		{
			Id = found.Id,
			Timestamp = Timestamp.FromDateTime(found.Timestamp),
			EncryptedText = ByteString.CopyFrom(found.EncryptedText),
			Iv = ByteString.CopyFrom(found.Iv),
			SenderId = found.SenderId,
			Attachments =
			{
				ToGrpcAttachments(found.Attachments)
			}
		};
	}
}