using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MongoDB.Driver;
using RedBox.Models;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

public partial class ConversationService
{
	[PermissionsRequired(DefaultPermissions.CreateChats)]
	public override async Task<ChatResponse> CreateChat(IdMessage request, ServerCallContext context)
	{
		using var session = await _mongoClient.StartSessionAsync();
		var userId = context.GetUser().Id;
		string chatId;
		string[] members;
		Timestamp timestamp;

		try
		{
			await session.AbortTransactionAsync();

			members = new[] { userId, request.Id };
			var chatDetail = new Chat
			{
				CreatedAt = DateTime.Now,
				MembersIds = members
			};

			await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
				.GetCollection<Chat>(_dbSettings.ChatDetailsCollection).InsertOneAsync(chatDetail);

			chatId = chatDetail.Id!;
			timestamp = Timestamp.FromDateTime(chatDetail.CreatedAt);

			await _mongoClient.GetDatabase(_dbSettings.ChatsDatabase).CreateCollectionAsync(chatId);

			var collection = _mongoClient.GetDatabase(_dbSettings.ChatsDatabase)
				.GetCollection<Message>(chatId);
			var indexes = new CreateIndexModel<Message>[]
			{
				new(Builders<Message>.IndexKeys.Descending(m => m.Timestamp)),
				new(Builders<Message>.IndexKeys.Ascending(m => m.UserDeleted)),
				new(Builders<Message>.IndexKeys.Ascending(m => m.ToRead))
			};
			await collection.Indexes.CreateManyAsync(indexes);

			var userCollection = _mongoClient.GetDatabase(_userDbSettings.DatabaseName)
				.GetCollection<User>(_userDbSettings.UsersCollection);

			await userCollection.UpdateManyAsync(Builders<User>.Filter.In(u => u.Id, members),
				Builders<User>.Update.Push(u => u.ChatIds, chatId));

			await session.CommitTransactionAsync();
		}
		catch (MongoException e)
		{
			return new ChatResponse
			{
				Result = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			};
		}

		var chat = new GrpcChat
		{
			Id = chatId,
			CreatedAt = timestamp,
			Members = { members }
		};

		await _clientsRegistry.NotifyOneAsync(request.Id, new ServerUpdate
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Chat = chat
		});

		return new ChatResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Chat = chat
		};
	}

	public override async Task<ChatResponse> GetChatFromId(IdMessage request, ServerCallContext context)
	{
		var userId = context.GetUser().Id;
		var chat = await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
			.Find(c => c.Id == request.Id && c.MembersIds.Contains(userId)).FirstAsync();

		if (chat is null)
			return new ChatResponse
			{
				Result = new Result
				{
					Status = Status.InvalidParameter,
					Error = "Invalid chat name"
				}
			};

		return new ChatResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Chat = new GrpcChat
			{
				Id = chat.Id,
				CreatedAt = Timestamp.FromDateTime(chat.CreatedAt),
				Members = { chat.MembersIds },
				Messages = { await GetChatMessagesAsync(chat.Id!) }
			}
		};
	}

	public override async Task<ChatsResponse> GetAllUserOwnChats(Empty request, ServerCallContext context)
	{
		return await base.GetAllUserOwnChats(request, context);
	}

	private string GetOtherChatUser(string collectionId, string userId)
	{
		return _mongoClient.GetDatabase(_dbSettings.DatabaseName).GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
			.Find(c => c.Id == collectionId).First().MembersIds.First(i => i != userId);
	}

	private async Task<GrpcMessage[]> GetChatMessagesAsync(string chatId, int chunk = 0)
	{
		var found = await _mongoClient.GetDatabase(_dbSettings.ChatsDatabase).GetCollection<Message>(chatId)
			.Find(m => !m.UserDeleted).SortByDescending(m => m.Timestamp)
			.Skip(chunk * _appSettings.MsgRetrieveChunkSize)
			.Limit(_appSettings.MsgRetrieveChunkSize).ToListAsync();

		var messages = new GrpcMessage[found.Count];
		for (var i = 0; i < found.Count; i++)
			messages[i] = new GrpcMessage
			{
				Id = found[i].Id,
				Timestamp = Timestamp.FromDateTime(found[i].Timestamp),
				EncryptedText = ByteString.CopyFrom(found[i].EncryptedText),
				Iv = ByteString.CopyFrom(found[i].Iv),
				ToRead = found[i].ToRead,
				SenderId = found[i].SenderId,
				Attachments = { ToGrpcAttachments(found[i].Attachments) }
			};

		return messages;
	}

	private async Task<bool> IsInChatAsync(string userId, string chatId)
	{
		return await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.ChatDetailsCollection)
			.Find(c => c.Id == chatId && c.MembersIds.Contains(userId)).CountDocumentsAsync() > 0;
	}
}