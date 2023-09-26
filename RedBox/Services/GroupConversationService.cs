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
	public override async Task<GroupResponse> GetUserGroupFromId(StringMessage request, ServerCallContext context)
	{
		if (string.IsNullOrEmpty(request.Value))
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
		var found = await groupsDetails.Find(g => g.Id == request.Value && g.MembersIds.Contains(context.GetUser().Id))
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

		var messages = await GetGroupMessagesAsync(request.Value);
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

	[PermissionsRequired(DefaultPermissions.CreateGroups)]
	public override async Task<GroupResponse> CreateGroup(GroupCreationRequest request, ServerCallContext context)
	{
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

	private IEnumerable<string> GetGroupMembers(string collectionId, string userId)
	{
		return _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection).Find(g => g.Id == collectionId).First()
			.MembersIds
			.Where(i => i != userId);
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
				SenderId = found[i].SenderId,
				Attachments = { ToGrpcAttachments(found[i].Attachments) }
			};

		return messages;
	}

	private async Task<bool> IsInGroupAsync(string userId, string groupId)
	{
		return await _mongoClient.GetDatabase(_dbSettings.DatabaseName)
			.GetCollection<Group>(_dbSettings.GroupDetailsCollection)
			.Find(g => g.Id == groupId && g.MembersIds.Contains(userId)).CountDocumentsAsync() > 0;
	}
}