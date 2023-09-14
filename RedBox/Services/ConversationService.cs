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
//		_chatDefinitions = defDb.GetCollection<Chat>(_dbSettings.ChatDetailsCollection);
//		_groupDefinitions = defDb.GetCollection<Group>(_dbSettings.GroupDetailsCollection);
	}

	[AuthenticationRequired]
	public override async Task Sync(IAsyncStreamReader<ClientUpdate> requestStream,
		IServerStreamWriter<ServerUpdate> responseStream, ServerCallContext context)
	{
		var userId = context.GetUser().Id;
		_clientsRegistry.Add(userId, responseStream);

		do
		{
			switch (requestStream.Current.OperationCase)
			{
				case ClientUpdate.OperationOneofCase.SentMessage:
					await NewMessage(requestStream.Current.SentMessage, userId, responseStream);
					break;
				case ClientUpdate.OperationOneofCase.DeletedMessages:
					break;
				case ClientUpdate.OperationOneofCase.CollectionToRead:
					break;
				case ClientUpdate.OperationOneofCase.None:
				default:
					throw new ArgumentOutOfRangeException(nameof(requestStream));
			}
		} while (await requestStream.MoveNext());
	}

	private async Task NewMessage(MessageOfCollection msgColl, string userId, IAsyncStreamWriter<ServerUpdate> response)
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

		if (await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			    .GetCollection<Chat>(_dbSettings.ChatDetailsCollection).Find(c => c.Id == msgColl.Collection.Chat)
			    .CountDocumentsAsync() == 0 && await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			    .GetCollection<Group>(_dbSettings.GroupDetailsCollection).Find(g => g.Id == msgColl.Collection.Group)
			    .CountDocumentsAsync() == 0)
		{
			await response.WriteAsync(new ServerUpdate
			{
				Result = new Result
				{
					Status = Status.InvalidParameter,
					Error = "Invalid collection "
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
					Status = Status.MessageTooBig,
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

				dbMessage.AttachmentIds = new string[msgColl.Message.Attachments.Count];

				session.StartTransaction();
				for (var i = 0; i < msgColl.Message.Attachments.Count; i++)
					unsafe
					{
						using var memory = msgColl.Message.Attachments[i].Data.Memory.Pin();
						using var stream = new UnmanagedMemoryStream((byte*)memory.Pointer,
							msgColl.Message.Attachments[i].Data.Length);
						var attachmentId = bucket.UploadFromStream(msgColl.Message.Attachments[i].Name, stream);

						var id = attachmentId.ToString()!;
						dbMessage.AttachmentIds[i] = id;
						msgColl.Message.Attachments[i].Id = id;
						msgColl.Message.Attachments[i].ClearData();
					}
			}

			if (msgColl.Collection.HasChat)
			{
				var chat = _mongoClient.GetDatabase(_dbSettings.ChatsDatabase)
					.GetCollection<Message>(msgColl.Collection.Chat);
				await chat.InsertOneAsync(dbMessage);
			}
			else
			{
				var group = _mongoClient.GetDatabase(_dbSettings.GroupsDatabase)
					.GetCollection<Message>(msgColl.Collection.Group);
				await group.InsertOneAsync(dbMessage);
			}

			await session.CommitTransactionAsync();
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

		msgColl.Message.Id = dbMessage.Id;

		var update = new ServerUpdate
		{
			Result = new Result
			{
				Status = Status.Ok
			},
			ReceivedMessage = msgColl
		};

		if (msgColl.Collection.HasChat)
		{
			var otherUserId =
				_mongoClient.GetDatabase(_dbSettings.DatabaseName)
					.GetCollection<Chat>(_dbSettings.ChatDetailsCollection)
					.Find(c => c.Id == msgColl.Collection.Chat).First().MembersIds!.First(i => i != userId);
			await _clientsRegistry.NotifyOneAsync(otherUserId, update);
		}
		else
		{
			var usersIds =
				_mongoClient.GetDatabase(_dbSettings.DatabaseName)
					.GetCollection<Group>(_dbSettings.GroupDetailsCollection)
					.Find(g => g.Id == msgColl.Collection.Group).First().MembersIds!.Where(i => i != userId);
			await _clientsRegistry.NotifyMultiAsync(usersIds, update);
		}
	}
}