using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RedBoxServices;

namespace RedBox.Services;

public partial class ChatService : GrpcConversationServices.GrpcConversationServicesBase
{
	public override async Task<ChatResponse> CreateChat(IdRequest request, ServerCallContext context)
	{
		return await base.CreateChat(request, context);
	}

	public override async Task<ChatResponse> GetUserChatFromId(IdRequest request, ServerCallContext context)
	{
		return await base.GetUserChatFromId(request, context);
	}

	public override async Task<ChatsResponse> GetAllUserChats(Empty request, ServerCallContext context)
	{
		return await base.GetAllUserChats(request, context);
	}
}