using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using keychain;
using RedBoxAuthentication;
using Xunit.Abstractions;
using Status = keychain.Status;

namespace RedBoxTests.Keychain;

public class UserKeyCreationServiceTest
{
	private const int RsaKeySize = 6144;
	private const int AesKeySize = 256;
	private readonly GrpcChannel? _channel;
	private readonly GrpcUserKeysCreationServices.GrpcUserKeysCreationServicesClient _client;
	private readonly ITestOutputHelper _console;
	private readonly Metadata _metadata;

	public UserKeyCreationServiceTest(ITestOutputHelper testOutputHelper)
	{
		_console = testOutputHelper;
		_channel = GrpcChannel.ForAddress(Common.RedBoxServerAddress);
		var login = new AuthenticationGrpcService.AuthenticationGrpcServiceClient(_channel);
		var res = login.Login(new LoginRequest
		{
			Username = Common.User,
			Password = Common.Password
		});

		_metadata = new Metadata
		{
			{ "Authorization", res.Token }
		};

		_channel = GrpcChannel.ForAddress(Common.KeychainServerAddress);
		_client = new GrpcUserKeysCreationServices.GrpcUserKeysCreationServicesClient(_channel);
	}

	[Fact]
	public async void CreateUserMasterKey()
	{
		var key = Common.CreateAesKey(Common.Password, out var iv);
		var result = await _client.CreateUserMasterKeyAsync(new MasterKey
		{
			EncryptedData = ByteString.CopyFrom(key),
			Iv = ByteString.CopyFrom(iv)
		}, _metadata);

		if (result.Status == Status.Error) _console.WriteLine(result.Error);
	}

	[Fact]
	public async void CreateUserKeyPair()
	{
		var keypair = Common.CreateKeyPair();

		var clientGet = new GrpcUserKeysRetrievingServices.GrpcUserKeysRetrievingServicesClient(_channel);
		var masterKey = clientGet.GetUserMasterKeyAsync(new Empty());
	}
}