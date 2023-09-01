using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RedBoxServices;

namespace RedBox.Services;

public partial class ChatService
{
	public override async Task<GroupResponse> GetUserGroupFromId(IdRequest request, ServerCallContext context)
	{
		return await base.GetUserGroupFromId(request, context);
	}

	public override async Task<GroupsResponse> GetAllUserGroups(Empty request, ServerCallContext context)
	{
		return await base.GetAllUserGroups(request, context);
	}

	public override async Task<GroupResponse> CreateGroup(GroupCreationRequest request, ServerCallContext context)
	{
		return await base.CreateGroup(request, context);
	}
}