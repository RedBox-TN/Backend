using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RedBox.Models;
using RedBox.Providers;
using RedBox.Settings;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using RedBoxServices;
using Shared;
using Status = Shared.Status;

namespace RedBox.Services;

[AuthenticationRequired]
public class ConversationService : GrpcConversationServices.GrpcConversationServicesBase
{
	private readonly RedBoxApplicationSettings _appSettings;
	private readonly IClientsRegistryProvider _clientsRegistry;
	private readonly RedBoxDatabaseSettings _dbSettings;
	private readonly IMongoClient _mongoClient;

	public ConversationService(IOptions<RedBoxDatabaseSettings> dbSettings,
		IOptions<RedBoxApplicationSettings> appSettings, IClientsRegistryProvider clientsRegistry)
	{
		_clientsRegistry = clientsRegistry;
		_dbSettings = dbSettings.Value;
		_appSettings = appSettings.Value;

		_mongoClient = new MongoClient(_dbSettings.ConnectionString);
	}

	[AuthenticationRequired]
	public override async Task Sync(IAsyncStreamReader<ClientUpdate> requestStream,
		IServerStreamWriter<ServerUpdate> responseStream, ServerCallContext context)
	{
		var userId = context.GetUser().Id;
		_clientsRegistry.Add(userId, responseStream);

		while (await requestStream.MoveNext())
			switch (requestStream.Current.OperationCase)
			{
				case ClientUpdate.OperationOneofCase.SentMessage:
					await NewMessage(requestStream.Current.SentMessage, responseStream, userId);
					break;
				case ClientUpdate.OperationOneofCase.DeletedMessages:
					await DeleteMessages(requestStream.Current.DeletedMessages, responseStream, userId);
					break;
				case ClientUpdate.OperationOneofCase.GetCollectionDetails:
					await GetCollectionDetails(requestStream.Current.GetCollectionDetails, responseStream, userId);
					break;
				case ClientUpdate.OperationOneofCase.None:
				default:
					throw new ArgumentOutOfRangeException(nameof(requestStream));
			}

		_clientsRegistry.Remove(userId);
	}

	private async Task NewMessage(MessageOfCollection msgColl,
		IServerStreamWriter<ServerUpdate> response, string userId)
	{
		if ((string.IsNullOrEmpty(msgColl.Collection.Chat) && string.IsNullOrEmpty(msgColl.Collection.Group)) ||
		    (msgColl.Message.EncryptedText.IsEmpty && msgColl.Message.Attachments.Count == 0) ||
		    msgColl.Message.Iv.IsEmpty)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.MissingParameters,
					Error =
						"Each message must contain one collection, an iv and at least some text or one attachment"
				}
			});
			return;
		}

		var dbMessage = new Message
		{
			SenderId = userId,
			ToRead = true,
			Iv = msgColl.Message.Iv.ToByteArray(),
			Timestamp = DateTime.Now
		};

		if (msgColl.Message.HasEncryptedText) dbMessage.EncryptedText = msgColl.Message.EncryptedText.ToByteArray();

		if (msgColl.Message.Attachments.Count > _appSettings.MaxAttachmentsPerMsg)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.InvalidParameter,
					Error = "Too many attachments"
				}
			});
			return;
		}


		foreach (var attachment in msgColl.Message.Attachments)
		{
			if (attachment.Data.IsEmpty)
			{
				await response.WriteAsync(new ServerUpdate
				{
					Result = new Result
					{
						Status = Status.InvalidParameter,
						Error = $"{attachment.Name} was empty"
					}
				});
				return;
			}

			if (attachment.Data.Length <= _appSettings.MaxAttachmentSizeMb * 1024 * 1024) continue;
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.AttachmentTooBig,
					Error = $"{attachment.Name} was too big"
				}
			});
			return;
		}

		using var session = await _mongoClient.StartSessionAsync();
		try
		{
			if (msgColl.Message.Attachments.Count > 0)
			{
				var bucket = new GridFSBucket(_mongoClient.GetDatabase(_dbSettings.GridFsDatabase),
					new GridFSBucketOptions
					{
						BucketName = msgColl.Collection.HasChat ? msgColl.Collection.Chat : msgColl.Collection.Group,
						ChunkSizeBytes = _dbSettings.GridFsChunkSizeBytes
					});

				dbMessage.Attachments = new Attachment[msgColl.Message.Attachments.Count];

				session.StartTransaction();
				for (var i = 0; i < msgColl.Message.Attachments.Count; i++)
					unsafe
					{
						using var memory = msgColl.Message.Attachments[i].Data.Memory.Pin();
						using var stream = new UnmanagedMemoryStream((byte*)memory.Pointer,
							msgColl.Message.Attachments[i].Data.Length);
						var attachmentId = bucket.UploadFromStream(msgColl.Message.Attachments[i].Name, stream);

						var id = attachmentId.ToString()!;
						dbMessage.Attachments[i].Id = id;
						dbMessage.Attachments[i].Name = msgColl.Message.Attachments[i].Name;

						msgColl.Message.Attachments[i].Id = id;
						msgColl.Message.Attachments[i].ClearData();
					}
			}

			if (msgColl.Collection.HasChat)
			{
				var chat = _mongoClient.GetDatabase(_dbSettings.ChatsDatabase)
					.GetCollection<Message>(msgColl.Collection.Chat);
				await chat.InsertOneAsync(dbMessage);
				await session.CommitTransactionAsync();

				msgColl.Message.Id = dbMessage.Id;
				var update = new ServerUpdate
				{
					Result = new Result
					{
						Status = Status.Ok
					},
					ReceivedMessage = msgColl
				};

				await _clientsRegistry.NotifyOneAsync(GetOtherChatUser(msgColl.Collection.Chat, userId), update);
			}
			else
			{
				var group = _mongoClient.GetDatabase(_dbSettings.GroupsDatabase)
					.GetCollection<Message>(msgColl.Collection.Group);
				await group.InsertOneAsync(dbMessage);
				await session.CommitTransactionAsync();

				msgColl.Message.Id = dbMessage.Id;
				var update = new ServerUpdate
				{
					Result = new Result
					{
						Status = Status.Ok
					},
					ReceivedMessage = msgColl
				};

				await _clientsRegistry.NotifyMultiAsync(GetGroupMembers(msgColl.Collection.Group, userId), update);
			}
		}
		catch (MongoException e)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			});

			await session.AbortTransactionAsync();
		}
	}

	private async Task DeleteMessages(DeleteMessagesRequest request, IServerStreamWriter<ServerUpdate> response,
		string userId)
	{
		if ((string.IsNullOrEmpty(request.Collection.Chat) && string.IsNullOrEmpty(request.Collection.Group)) ||
		    request.MessageIds.Count == 0)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.MissingParameters,
					Error = "Each request must contains at least one message id and the collection in which it belongs"
				}
			});
			return;
		}

		var chat = _mongoClient.GetDatabase(_dbSettings.ChatsDatabase).GetCollection<Message>(request.Collection.Chat);
		var group = _mongoClient.GetDatabase(_dbSettings.GroupsDatabase)
			.GetCollection<Message>(request.Collection.Group);

		try
		{
			if (request.Collection.HasChat)
			{
				await chat.UpdateManyAsync(Builders<Message>.Filter.In(m => m.Id, request.MessageIds),
					Builders<Message>.Update.Set(m => m.UserDeleted, true));

				await _clientsRegistry.NotifyOneAsync(GetOtherChatUser(request.Collection.Chat, userId),
					new ServerUpdate
					{
						Result = new Result
						{
							Status = Status.Ok
						},
						DeletedMessages = new DeleteMessagesRequest
						{
							Collection = new Collection
							{
								Chat = request.Collection.Chat
							}
						}
					});
			}
			else
			{
				await group.UpdateManyAsync(Builders<Message>.Filter.In(m => m.Id, request.MessageIds),
					Builders<Message>.Update.Set(m => m.UserDeleted, true));

				await _clientsRegistry.NotifyMultiAsync(GetGroupMembers(request.Collection.Group, userId),
					new ServerUpdate
					{
						Result = new Result
						{
							Status = Status.Ok
						},
						DeletedMessages = new DeleteMessagesRequest
						{
							Collection = new Collection
							{
								Chat = request.Collection.Chat
							}
						}
					});
			}
		}
		catch (MongoException e)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			});
			return;
		}

		await response.WriteAsync(new ServerUpdate
		{
			Result = new Result
			{
				Status = Status.Ok
			}
		});
	}

	private async Task GetCollectionDetails(Collection request, IServerStreamWriter<ServerUpdate> response,
		string userId)
	{
		if (string.IsNullOrEmpty(request.Chat) && string.IsNullOrEmpty(request.Group))
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.MissingParameters,
					Error = "Each request must contains one chat or group id"
				}
			});

			return;
		}

		var update = new ServerUpdate
		{
			Result = new Result
			{
				Status = Status.Ok
			}
		};

		try
		{
			if (request.HasChat)
			{
				var chat = await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
					.GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
					.Find(g => g.Id == request.Chat && g.MembersIds!.Contains(userId)).FirstAsync();

				update.Chat = new GrpcChat
				{
					Id = chat.Id,
					CreatedAt = Timestamp.FromDateTime(chat.CreatedAt),
					Members = { chat.MembersIds },
					Messages = { await GetChatMessagesAsync(chat.Id!) }
				};
			}
			else
			{
				var group = await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
					.GetCollection<Group>(_dbSettings.GroupDetailsCollection)
					.Find(g => g.Id == request.Group && g.MembersIds!.Contains(userId)).FirstAsync();

				update.Group = new GrpcGroup
				{
					Id = group.Id,
					CreatedAt = Timestamp.FromDateTime(group.CreatedAt),
					Name = group.Name,
					Admins = { group.AdminsIds },
					Members = { group.MembersIds },
					Messages = { await GetGroupMessagesAsync(group.Id!) }
				};
			}
		}
		catch (MongoException e)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			});
			return;
		}

		await response.WriteAsync(update);
	}

	private IEnumerable<string> GetGroupMembers(string collectionId, string userId)
	{
		return _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection).Find(g => g.Id == collectionId).First()
			.MembersIds!
			.Where(i => i != userId);
	}

	private string GetOtherChatUser(string collectionId, string userId)
	{
		return _mongoClient.GetDatabase(_dbSettings.DatabaseName).GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
			.Find(c => c.Id == collectionId).First().MembersIds!.First(i => i != userId);
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

	private async Task<GrpcMessage[]> GetGroupMessagesAsync(string groupId, int chunk = 0)
	{
		var found = await _mongoClient.GetDatabase(_dbSettings.GroupsDatabase).GetCollection<Message>(groupId)
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
}