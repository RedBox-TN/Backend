using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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
using Status = Grpc.Core.Status;

namespace RedBox.Services;

[AuthenticationRequired]
public partial class ConversationService : GrpcConversationServices.GrpcConversationServicesBase
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
					throw new RpcException(new Status(StatusCode.InvalidArgument, nameof(requestStream)));
			}

		_clientsRegistry.Remove(userId);
	}

	public override async Task<ChunkResponse> GetMessagesInRange(MessageChunkRequest request,
		ServerCallContext context)
	{
		try
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
					Status = Shared.Status.Ok
				},
				Messages =
				{
					result
				}
			};
		}
		catch (Exception e)
		{
			return new ChunkResponse
			{
				Result = new Result
				{
					Status = Shared.Status.Error,
					Error = e.Message
				}
			};
		}
	}

	public override async Task<GrpcAttachment> GetAttachmentData(AttachmentRequest request, ServerCallContext context)
	{
		try
		{
			var bucket = new GridFSBucket(_mongoClient.GetDatabase(_dbSettings.GridFsDatabase),
				new GridFSBucketOptions
				{
					BucketName = request.BucketName,
					ChunkSizeBytes = _dbSettings.GridFsChunkSizeBytes
				});

			var fileMetadata =
				await (await bucket.FindAsync(
						Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, new ObjectId(request.FileId))))
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
		catch (Exception e)
		{
			throw new RpcException(new Status(StatusCode.Internal, e.Message));
		}
	}

	[PermissionsRequired(DefaultPermissions.CreateChats)]
	public override async Task<AvailableUsersResponse> GetUsersForConversation(Empty request, ServerCallContext context)
	{
		try
		{
			var user = context.GetUser();
			List<User> found;


			var currentChats = await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
				.GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
				.Find(Builders<Chat>.Filter.AnyEq(x => x.MembersIds, user.Id)).ToListAsync();

			if (currentChats.Count > 0)
			{
				var excluded = currentChats.Select(c => c.MembersIds.First(u => u != user.Id));

				found = await _mongoClient.GetDatabase(_userDbSettings.DatabaseName)
					.GetCollection<User>(_userDbSettings.UsersCollection)
					.Find(Builders<User>.Filter.Not(Builders<User>.Filter.In(u => u.Id, excluded))).ToListAsync();
			}
			else
			{
				found = await _mongoClient.GetDatabase(_userDbSettings.DatabaseName)
					.GetCollection<User>(_userDbSettings.UsersCollection)
					.Find(u => u.Id != user.Id).ToListAsync();
			}

			if (found.Count == 0)
				return new AvailableUsersResponse
				{
					Result = new Result
					{
						Status = Shared.Status.Ok
					},
					Users =
					{
						Array.Empty<UserInfo>()
					}
				};

			var users = new UserInfo[found.Count];
			for (var i = 0; i < found.Count; i++)
				users[i] = new UserInfo
				{
					Id = found[i].Id,
					Name = found[i].Name,
					Surname = found[i].Surname,
					Email = found[i].Email,
					Username = found[i].Username,
					Biography = found[i].Biography
				};

			return new AvailableUsersResponse
			{
				Result = new Result
				{
					Status = Shared.Status.Ok
				},
				Users =
				{
					users
				}
			};
		}
		catch (Exception e)
		{
			return new AvailableUsersResponse
			{
				Result = new Result
				{
					Error = e.Message,
					Status = Shared.Status.Error
				}
			};
		}
	}

	private static IEnumerable<GrpcAttachment> ToGrpcAttachments(Attachment[]? attachments)
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
		IAsyncStreamWriter<ServerUpdate> response, string userId)
	{
		if ((string.IsNullOrEmpty(msgColl.Collection.Chat) && string.IsNullOrEmpty(msgColl.Collection.Group)) ||
		    (msgColl.Message.EncryptedText.IsEmpty && msgColl.Message.Attachments.Count == 0) ||
		    msgColl.Message.Iv.IsEmpty)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Shared.Status.MissingParameters,
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
					Status = Shared.Status.InvalidParameter,
					Error =
						"Invalid collection name"
				}
			});
			return;
		}

		var dbMessage = new Message
		{
			SenderId = userId,
			Iv = msgColl.Message.Iv.ToByteArray(),
			Timestamp = DateTime.UtcNow
		};

		if (msgColl.Message.HasEncryptedText) dbMessage.EncryptedText = msgColl.Message.EncryptedText.ToByteArray();

		if (msgColl.Message.Attachments.Count > _appSettings.MaxAttachmentsPerMsg)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Shared.Status.InvalidParameter,
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
						Status = Shared.Status.InvalidParameter,
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
					Status = Shared.Status.AttachmentTooBig,
					Error = $"{attachment.Name} was too big"
				}
			});
			return;
		}

		var update = new ServerUpdate
		{
			Result = new Result
			{
				Status = Shared.Status.Ok
			}
		};
		using var session = await _mongoClient.StartSessionAsync();
		try
		{
			session.StartTransaction();

			if (msgColl.Collection.HasChat)
			{
				var chat = _mongoClient.GetDatabase(_dbSettings.ChatsDatabase)
					.GetCollection<Message>(msgColl.Collection.Chat);
				await chat.InsertOneAsync(dbMessage);

				msgColl.Message.Id = dbMessage.Id;
				update.ReceivedMessage = msgColl;
			}
			else
			{
				var group = _mongoClient.GetDatabase(_dbSettings.GroupsDatabase)
					.GetCollection<Message>(msgColl.Collection.Group);
				await group.InsertOneAsync(dbMessage);

				msgColl.Message.Id = dbMessage.Id;
				update.ReceivedMessage = msgColl;
			}

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
				{
					var attachmentId = await bucket.UploadFromBytesAsync(msgColl.Message.Attachments[i].Name,
						msgColl.Message.Attachments[i].ToByteArray());

					var id = attachmentId.ToString()!;
					dbMessage.Attachments[i].Id = id;
					dbMessage.Attachments[i].Name = msgColl.Message.Attachments[i].Name;

					msgColl.Message.Attachments[i].Id = id;
					msgColl.Message.Attachments[i].ClearData();
				}
			}

			await session.CommitTransactionAsync();
		}
		catch (Exception e)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Shared.Status.Error,
					Error = e.Message
				}
			});

			await session.AbortTransactionAsync();
		}

		if (msgColl.Collection.HasChat)
			await _clientsRegistry.NotifyOneAsync(GetOtherChatUser(msgColl.Collection.Chat, userId), update);
		else
			await _clientsRegistry.NotifyMultiAsync(GetGroupMembers(msgColl.Collection.Group, userId), update);
	}

	private async Task DeleteMessages(DeleteMessagesRequest request, IAsyncStreamWriter<ServerUpdate> response,
		string userId)
	{
		if ((string.IsNullOrEmpty(request.Collection.Chat) && string.IsNullOrEmpty(request.Collection.Group)) ||
		    request.MessageIds.Count == 0)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Shared.Status.MissingParameters,
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
							Status = Shared.Status.Ok
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
							Status = Shared.Status.Ok
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
		catch (Exception e)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Shared.Status.Error,
					Error = e.Message
				}
			});
			return;
		}

		await response.WriteAsync(new ServerUpdate
		{
			Result = new Result
			{
				Status = Shared.Status.Ok
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
					Status = Shared.Status.MissingParameters,
					Error = "Each request must contains one chat or group id"
				}
			});

			return;
		}

		var update = new ServerUpdate
		{
			Result = new Result
			{
				Status = Shared.Status.Ok
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
					Members =
					{
						chat.MembersIds
					},
					Messages =
					{
						await GetChatMessagesAsync(chat.Id!)
					}
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
					Admins =
					{
						group.AdminsIds
					},
					Members =
					{
						group.MembersIds
					},
					Messages =
					{
						await GetGroupMessagesAsync(group.Id!)
					}
				};
			}
		}
		catch (Exception e)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Shared.Status.Error,
					Error = e.Message
				}
			});
			return;
		}

		await response.WriteAsync(update);
	}

	private async Task<GrpcMessage?> GetMessageFromCollectionAsync(string collectionId, string? messageId = null,
		bool isGroup = false)
	{
		try
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
		catch (Exception e)
		{
			throw new RpcException(new Status(StatusCode.Internal, e.Message));
		}
	}
}