using Grpc.Core;
using Grpc.Net.Client;
using RedBoxAuthentication;
using RedBoxServices;
using Xunit.Priority;

namespace RedBoxTests.Conversation;

[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public class Conversation
{
	private readonly GrpcConversationServices.GrpcConversationServicesClient _client;
	private readonly Metadata _metadata;

	public Conversation()
	{
		var channel = GrpcChannel.ForAddress(Common.RedBoxServerAddress);
		var login = new AuthenticationGrpcService.AuthenticationGrpcServiceClient(channel);

		var res = login.Login(new LoginRequest
		{
			Username = Common.AdminUser,
			Password = Common.Password
		});

		_metadata = new Metadata
		{
			{ "Authorization", res.Token }
		};

		channel = GrpcChannel.ForAddress(Common.KeychainServerAddress);
		_client = new GrpcConversationServices.GrpcConversationServicesClient(channel);
	}
}