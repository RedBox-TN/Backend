using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Settings;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using RedBoxAuth.Settings;
using RedBoxDummy;
using Shared.Models;

namespace RedBox.Services;

[RequiredPermissions(DefaultPermissions.CreateChats)]
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