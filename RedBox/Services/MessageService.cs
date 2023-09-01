using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RedBoxServices;
using Shared;

namespace RedBox.Services;

public partial class ChatService
{
	public override async Task<MessagesResponse> GetOldMessages(OldMessagesRequest request, ServerCallContext context)
	{
		return await base.GetOldMessages(request, context);
	}

	public override async Task<ReceivedMessagesResponse> GetNewMessages(Empty request, ServerCallContext context)
	{
		return await base.GetNewMessages(request, context);
	}

	public override async Task<Result> SendMessage(MessageCreationRequest request, ServerCallContext context)
	{
		return await base.SendMessage(request, context);
	}

	public override async Task<Result> MarkMessageAsRead(MessageFromIdRequest request, ServerCallContext context)
	{
		return await base.MarkMessageAsRead(request, context);
	}

	public override async Task<Result> DeleteMessages(DeleteMessagesRequest request, ServerCallContext context)
	{
		return await base.DeleteMessages(request, context);
	}
}