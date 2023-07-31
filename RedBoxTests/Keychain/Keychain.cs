// using System.Runtime.Versioning;
// using System.Security.Cryptography;
// using Google.Protobuf;
// using Grpc.Core;
// using Grpc.Net.Client;
// using keychain;
// using MongoDB.Bson;
// using RedBoxAuthentication;
// using Xunit.Abstractions;
// using Status = keychain.Status;
//
// namespace RedBoxTests.Keychain;
//
// [UnsupportedOSPlatform("windows")]
// public class Keychain
// {
// 	private const int RsaKeySize = 4096;
// 	private const int AesKeySize = 256;
// 	private readonly GrpcKeysCreationServices.GrpcKeysCreationServicesClient _client;
// 	private readonly Metadata _metadata;
// 	private readonly ITestOutputHelper _testOutputHelper;
//
// 	public Keychain(ITestOutputHelper testOutputHelper)
// 	{
// 		_testOutputHelper = testOutputHelper;
// 		var channel = GrpcChannel.ForAddress(Common.RedBoxServerAddress);
// 		var login = new AuthenticationGrpcService.AuthenticationGrpcServiceClient(channel);
// 		var res = login.Login(new LoginRequest
// 		{
// 			Username = Common.User,
// 			Password = Common.Password
// 		});
//
// 		_metadata = new Metadata
// 		{
// 			{ "Authorization", res.Token }
// 		};
//
// 		channel = GrpcChannel.ForAddress(Common.KeychainServerAddress, new GrpcChannelOptions());
// 		_client = new GrpcInsertKeyServices.GrpcInsertKeyServicesClient(channel);
// 	}
//
// 	[Fact]
// 	public async void InsertUserKeys()
// 	{
// 		CreateKeyPair(out var priv, out var pub);
//
// 		var response = await _client.InsertUserKeysAsync(new InsertUserKeysRequest
// 		{
// 			PrivateKey = ByteString.CopyFrom(priv),
// 			PublicKey = ByteString.CopyFrom(pub)
// 		}, _metadata);
//
// 		Assert.True(response.Status == Status.Ok);
// 	}
//
// 	[Fact]
// 	public async void InsertGroupKeys()
// 	{
// 		CreateKeyPair(out _, out var pub);
//
// 		var rsa = RSA.Create();
// 		rsa.ImportRSAPublicKey(pub, out _);
//
// 		var key = CreateAesKey();
//
// 		var response = await _client.InsertGroupKeysAsync(new InsertGroupKeysRequest
// 		{
// 			GroupId = ObjectId.GenerateNewId().ToString(),
// 			CreatorKey = ByteString.CopyFrom(key),
// 			MembersKeys =
// 			{
// 				new KeyMessage[]
// 				{
// 					new()
// 					{
// 						UserId = ObjectId.GenerateNewId().ToString(),
// 						Data = ByteString.CopyFrom(rsa.Encrypt(key, RSAEncryptionPadding.Pkcs1))
// 					}
// 				}
// 			}
// 		}, _metadata);
//
// 		Assert.True(response.Status == Status.Ok);
// 	}
//
// 	[Fact]
// 	public async void InsertSupervisorKeys()
// 	{
// 		CreateKeyPair(out var priv, out var pub);
//
// 		var response = await _client.InsertSupervisorKeysAsync(new InsertSupervisorKeysRequest
// 		{
// 			PrivateKey = ByteString.CopyFrom(priv),
// 			PublicKey = ByteString.CopyFrom(pub)
// 		}, _metadata);
//
// 		Assert.True(response.Status == Status.Ok);
// 	}
//
// 	private static void CreateKeyPair(out byte[] priv, out byte[] pub)
// 	{
// 		using var crypto = RSA.Create(RsaKeySize);
// 		priv = crypto.ExportRSAPrivateKey();
// 		pub = crypto.ExportRSAPublicKey();
// 	}
//
// 	private static byte[] CreateAesKey()
// 	{
// 		using var aes = Aes.Create();
// 		aes.KeySize = AesKeySize;
// 		aes.GenerateKey();
// 		return aes.Key;
// 	}
// }

