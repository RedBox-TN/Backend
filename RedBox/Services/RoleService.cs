using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Settings;
using RedBoxServices;
using Shared;

namespace RedBox.Services;

public class RoleService : GrpcRoleService.GrpcRoleServiceBase
{
    
    private readonly IMongoDatabase _database;
    private readonly AccountDatabaseSettings _databaseSettings;

    public RoleService(IOptions<AccountDatabaseSettings> options)
    {
        _databaseSettings = options.Value;
        var mongodbClient = new MongoClient(_databaseSettings.ConnectionString);
        _database = mongodbClient.GetDatabase(_databaseSettings.DatabaseName);
    }
    
    public override async Task<Result> CreateRole(GrpcRole request, ServerCallContext context)
    {
        return await base.CreateRole(request, context);
    }

    public override async Task<Result> DeleteRole(GrpcRoleIdentifier request, ServerCallContext context)
    {
        return await base.DeleteRole(request, context);
    }

    public override async Task<Result> ModifyRole(GrpcRole request, ServerCallContext context)
    {
        return await base.ModifyRole(request, context);
    }

    public override async Task<GrpcRoleResult> FetchRole(GrpcRoleIdentifier request, ServerCallContext context)
    {
        return await base.FetchRole(request, context);
    }

    public override async Task<GrpcRoleResults> FetchAllRoles(Empty request, ServerCallContext context)
    {
        return await base.FetchAllRoles(request, context);
    }
}