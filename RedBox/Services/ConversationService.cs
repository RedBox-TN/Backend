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
using RedBoxAuth.Settings;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

[AuthenticationRequired]
public class ConversationService : GrpcConversationServices.GrpcConversationServicesBase
{
	private readonly RedBoxApplicationSettings _appSettings;
	private readonly IClientsRegistryProvider _clientsRegistry;
	private readonly RedBoxDatabaseSettings _dbSettings;
	private readonly IMongoClient _mongoClient;
	private readonly AccountDatabaseSettings _userDbSettings;

	public ConversationService(IOptions<RedBoxDatabaseSettings> dbSettings,
		IOptions<RedBoxApplicationSettings> appSettings, IClientsRegistryProvider clientsRegistry,
		IOptions<AccountDatabaseSettings> userDbSettings)
	{
		_clientsRegistry = clientsRegistry;
		_userDbSettings = userDbSettings.Value;
		_dbSettings = dbSettings.Value;
		_appSettings = appSettings.Value;

		_mongoClient = new MongoClient(_dbSettings.ConnectionString);
	}

	public override async Task Sync(IAsyncStreamReader<ClientUpdate> requestStream,
		IServerStreamWriter<ServerUpdate> responseStream, ServerCallContext context)
	{
		var userId = context.GetUser().Id;
		_clientsRegistry.Add(userId, responseStream);

		while (await requestStream.MoveNext())
			switch (requestStream.Current.OperationCase)
			{
				case ClientUpdate.OperationOneofCase.SentMessage:
					await SendMessage(requestStream.Current.SentMessage, responseStream, userId);
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

	public override async Task<GroupResponse> GetUserGroupFromId(IdMessage request, ServerCallContext context)
	{
		if (string.IsNullOrEmpty(request.Id))
			return new GroupResponse
			{
				Result = new Result
				{
					Status = Status.MissingParameters,
					Error = "Request must contains an id of a valid group"
				}
			};

		var groupsDetails = _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection);
		var found = await groupsDetails.Find(g => g.Id == request.Id && g.MembersIds.Contains(context.GetUser().Id))
			.FirstAsync();

		if (found is null)
			return new GroupResponse
			{
				Result = new Result
				{
					Status = Status.InvalidParameter,
					Error = "Invalid id"
				}
			};

		var messages = await GetGroupMessagesAsync(request.Id);
		return new GroupResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Group = new GrpcGroup
			{
				Id = found.Id,
				CreatedAt = Timestamp.FromDateTime(found.CreatedAt),
				Name = found.Name,
				Admins = { found.AdminsIds },
				Members = { found.MembersIds },
				Messages = { messages }
			}
		};
	}

	public override async Task<GroupsResponse> GetAllUserGroups(Empty request, ServerCallContext context)
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
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection).Find(Builders<Group>.Filter.In(g => g.Id, ids))
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
				Messages = { await GetMessageFromCollection(found[i].Id!, isGroup: true) }
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

	public override async Task<GroupResponse> CreateGroup(GroupCreationRequest request, ServerCallContext context)
	{
		//todo controlli

		string groupId;
		Timestamp timestamp;
		string[] members;

		using var session = await _mongoClient.StartSessionAsync();
		try
		{
			members = request.Members.ToArray();
			var groupDetails = new Group
			{
				Name = request.Name,
				CreatedAt = DateTime.Now,
				AdminsIds = request.Admins.ToArray(),
				MembersIds = members
			};

			session.StartTransaction();
			await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
				.GetCollection<Group>(_dbSettings.GroupDetailsCollection).InsertOneAsync(groupDetails);

			groupId = groupDetails.Id!;
			timestamp = Timestamp.FromDateTime(groupDetails.CreatedAt);

			await _mongoClient.GetDatabase(_dbSettings.GroupsDatabase).CreateCollectionAsync(groupDetails.Id);
			var collection = _mongoClient.GetDatabase(_dbSettings.GroupsDatabase)
				.GetCollection<Message>(groupDetails.Id);
			var indexes = new CreateIndexModel<Message>[]
			{
				new(Builders<Message>.IndexKeys.Descending(m => m.Timestamp)),
				new(Builders<Message>.IndexKeys.Ascending(m => m.UserDeleted))
			};
			await collection.Indexes.CreateManyAsync(indexes);

			var userCollection = _mongoClient.GetDatabase(_userDbSettings.DatabaseName)
				.GetCollection<User>(_userDbSettings.UsersCollection);

			await userCollection.UpdateManyAsync(Builders<User>.Filter.In(u => u.Id, request.Members),
				Builders<User>.Update.Push(u => u.GroupIds, groupDetails.Id));

			await session.CommitTransactionAsync();
		}
		catch (MongoException e)
		{
			await session.AbortTransactionAsync();
			return new GroupResponse
			{
				Result = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			};
		}

		var group = new GrpcGroup
		{
			Id = groupId,
			Name = request.Name,
			CreatedAt = timestamp,
			Members = { request.Members },
			Admins = { request.Admins }
		};

		await _clientsRegistry.NotifyMultiAsync(members.Where(i => i != context.GetUser().Id), new ServerUpdate
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Group = group
		});

		return new GroupResponse
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			Group = group
		};
	}

	public override async Task<ChatResponse> CreateChat(IdMessage request, ServerCallContext context)
	{
		return await base.CreateChat(request, context);
	}

	public override async Task<ChatResponse> GetChatFromId(IdMessage request, ServerCallContext context)
	{
		return await base.GetChatFromId(request, context);
	}

	public override async Task<ChatsResponse> GetAllUserOwnChats(Empty request, ServerCallContext context)
	{
		return await base.GetAllUserOwnChats(request, context);
	}

	public override async Task<BucketResponse> GetMessagesInRange(MessageChunkRequest request,
		ServerCallContext context)
	{
		return await base.GetMessagesInRange(request, context);
	}

	public override async Task<GrpcAttachment> GetAttachment(AttachmentRequest request, ServerCallContext context)
	{
		return await base.GetAttachment(request, context);
	}

	private IEnumerable<string> GetGroupMembers(string collectionId, string userId)
	{
		return _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection).Find(g => g.Id == collectionId).First()
			.MembersIds
			.Where(i => i != userId);
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

	private GrpcAttachment[] ToGrpcAttachments(Attachment[]? attachments)
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

	private async Task SendMessage(MessageOfCollection msgColl,
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

		if ((msgColl.Collection.HasChat && !await IsInChatAsync(userId, msgColl.Collection.Chat)) ||
		    !await IsInGroupAsync(userId, msgColl.Collection.Group))
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.InvalidParameter,
					Error =
						"Invalid collection name"
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
			session.StartTransaction();
			if (msgColl.Message.Attachments.Count > 0)
			{
				var bucket = new GridFSBucket(_mongoClient.GetDatabase(_dbSettings.GridFsDatabase),
					new GridFSBucketOptions
					{
						BucketName = msgColl.Collection.HasChat ? msgColl.Collection.Chat : msgColl.Collection.Group,
						ChunkSizeBytes = _dbSettings.GridFsChunkSizeBytes
					});

				dbMessage.Attachments = new Attachment[msgColl.Message.Attachments.Count];

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
					.Find(g => g.Id == request.Chat && g.MembersIds.Contains(userId)).FirstAsync();

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
					.Find(g => g.Id == request.Group && g.MembersIds.Contains(userId)).FirstAsync();

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

	private async Task<bool> IsInGroupAsync(string userId, string groupId)
	{
		return await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection)
			.Find(g => g.Id == groupId && g.MembersIds.Contains(userId)).CountDocumentsAsync() > 0;
	}

	private async Task<bool> IsInChatAsync(string userId, string chatId)
	{
		return await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.ChatDetailsCollection)
			.Find(c => c.Id == chatId && c.MembersIds.Contains(userId)).CountDocumentsAsync() > 0;
	}

	private async Task<GrpcMessage?> GetMessageFromCollection(string collectionId, string? messageId = null,
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
			ToRead = found.ToRead,
			SenderId = found.SenderId,
			Attachments = { ToGrpcAttachments(found.Attachments) }
		};
	}
}