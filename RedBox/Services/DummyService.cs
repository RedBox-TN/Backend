using Grpc.Core;
using RedBoxAuth.Authorization;
using RedBoxDummy;
using Shared.Models;

namespace RedBox.Services;

[PermissionsRequired(DefaultPermissions.CreateChats)]
public class DummyService : DummyGrpcService.DummyGrpcServiceBase
{
	public override Task<HelloResponse> SayHello(Nil request, ServerCallContext context)
	{
		return Task.FromResult(new HelloResponse
		{
			Response = "ciao"
		});
	}
}