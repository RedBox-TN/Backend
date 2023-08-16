using keychain;
using Keychain.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;

namespace Keychain.Services;

[AuthenticationRequired]
public partial class KeychainServices : GrpcKeychainServices.GrpcKeychainServicesBase
{
    private readonly IMongoDatabase _database;
    private readonly DatabaseSettings _settings;

    public KeychainServices(IOptions<DatabaseSettings> options)
    {
        _settings = options.Value;
        var mongodbClient = new MongoClient(options.Value.ConnectionString);
        _database = mongodbClient.GetDatabase(options.Value.DatabaseName);
    }
}