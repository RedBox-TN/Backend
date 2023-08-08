using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using keychain;
using RedBoxAuthentication;
using Xunit.Abstractions;
using Xunit.Priority;

namespace RedBoxTests.Keychain;

[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public class UserKeyCreationServiceTest
{
	private readonly GrpcSupervisorKeysRetrievingServices.GrpcSupervisorKeysRetrievingServicesClient
		_clientSupervisorGet;

	private readonly GrpcSupervisorKeysCreationServices.GrpcSupervisorKeysCreationServicesClient _clientSupervisorSet;
	private readonly GrpcUserKeysRetrievingServices.GrpcUserKeysRetrievingServicesClient _clientUserGet;
	private readonly GrpcUserKeysCreationServices.GrpcUserKeysCreationServicesClient _clientUserSet;
	private readonly ITestOutputHelper _console;
	private readonly Metadata _metadata;

	public UserKeyCreationServiceTest(ITestOutputHelper testOutputHelper)
	{
		_console = testOutputHelper;
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
		_clientSupervisorGet =
			new GrpcSupervisorKeysRetrievingServices.GrpcSupervisorKeysRetrievingServicesClient(channel);
		_clientSupervisorSet = new GrpcSupervisorKeysCreationServices.GrpcSupervisorKeysCreationServicesClient(channel);
		_clientUserSet = new GrpcUserKeysCreationServices.GrpcUserKeysCreationServicesClient(channel);
		_clientUserGet = new GrpcUserKeysRetrievingServices.GrpcUserKeysRetrievingServicesClient(channel);
	}

	[Fact]
	[Priority(-1)]
	public async void CreateUserMasterKey()
	{
		var key = Common.CreateAesKey();

		var encKey = await Common.AesEncryptAsync(key, Common.Hash(Common.Password));
		var result = await _clientUserSet.CreateUserMasterKeyAsync(new MasterKey
		{
			EncryptedData = ByteString.CopyFrom(encKey.EncData),
			Iv = ByteString.CopyFrom(encKey.Iv)
		}, _metadata);

		if (result.HasError) Assert.Fail(result.Error);
	}

	[Fact]
	[Priority(1)]
	public async void CreateUserKeyPair()
	{
		var keypair = Common.CreateKeyPair();

		var response = await _clientUserGet.GetUserMasterKeyAsync(new Empty(), _metadata);

		if (!response.HasData || !response.HasIv) Assert.Fail("Missing master key or iv");

		var masterKey = await Common.AesDecryptAsync(response.Data.ToByteArray(),
			Common.Hash(Common.Password), response.Iv.ToByteArray());
		var encPrivKey = await Common.AesEncryptAsync(keypair.PrivKey, masterKey);

		var result = await _clientUserSet.CreateUserKeyPairAsync(new UserKeyPairCreationRequest
		{
			PublicKey = ByteString.CopyFrom(keypair.PubKey),
			EncryptedPrivateKey = ByteString.CopyFrom(encPrivKey.EncData),
			Iv = ByteString.CopyFrom(encPrivKey.Iv)
		}, _metadata);

		if (result.HasError) Assert.Fail(result.Error);
	}

	[Fact]
	[Priority(2)]
	public async void CreateChatKey()
	{
		var chatKey = Common.CreateAesKey();
		var userPubKey = await _clientUserGet.GetUserPublicKeyAsync(new KeyFromIdRequest
		{
			Id = Common.UserId
		}, _metadata);

		var encrypted = Common.RsaEncrypt(chatKey, userPubKey.Data.ToByteArray());

		var result = await _clientUserSet.CreateChatKeysAsync(new ChatKeyCreationRequest());
	}
}