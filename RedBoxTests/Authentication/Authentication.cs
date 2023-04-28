using Grpc.Core;
using Grpc.Net.Client;
using RedBoxAuthentication;
using Xunit.Abstractions;

namespace RedBoxTests.Authentication;

public class Authentication
{
	private readonly AuthenticationGrpcService.AuthenticationGrpcServiceClient _client;

	private readonly ITestOutputHelper _testOutputHelper;

	public Authentication(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
		var channel = GrpcChannel.ForAddress(Common.ServerAddress);
		_client = new AuthenticationGrpcService.AuthenticationGrpcServiceClient(channel);
	}


	private async Task<string> Login()
	{
		var response = await _client.LoginAsync(new LoginRequest
		{
			Email = "admin@redbox.it",
			Password = "password"
		});
		return response.Token;
	}

	[Fact]
	public async void LoginTest()
	{
		var token = await Login();
		Logout(token);
		Assert.False(token is null);
	}

	[Fact]
	public async void RefreshToken()
	{
		var token = await Login();
		var met = new Metadata
		{
			{ "Authorization", token }
		};
		var response = await _client.RefreshTokenAsync(new Nil(), met);

		Assert.True(token != response.Token);
		Logout(token);
	}

	[Fact]
	public async void LogoutTest()
	{
		var token = await Login();
		Logout(token);
	}

	private async void Logout(string token)
	{
		var response = await _client.LogoutAsync(new Nil(), new Metadata
		{
			{ "Authorization", token }
		});
		Assert.IsType<Nil>(response);
	}
}