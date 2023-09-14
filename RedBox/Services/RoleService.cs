using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.PermissionUtility;
using RedBoxAuth.Authorization;
using RedBoxAuth.Settings;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

public class RoleService : GrpcRoleService.GrpcRoleServiceBase
{
	private readonly IMongoDatabase _database;
	private readonly AccountDatabaseSettings _databaseSettings;
	private readonly IPermissionUtility _permissionUtility;

	public RoleService(IOptions<AccountDatabaseSettings> options, IPermissionUtility permissionUtility)
	{
		_databaseSettings = options.Value;
		var mongodbClient = new MongoClient(_databaseSettings.ConnectionString);
		_database = mongodbClient.GetDatabase(_databaseSettings.DatabaseName);
		_permissionUtility = permissionUtility;
	}

	[PermissionsRequired(DefaultPermissions.ManageRoles)]
	public override async Task<Result> CreateRole(GrpcRole request, ServerCallContext context)
	{
		var collection = _database.GetCollection<Role>(_databaseSettings.RolesCollection);

		if (
			!_permissionUtility.IsCodeCorrect(request.Permissions) ||
			string.IsNullOrEmpty(request.Description) ||
			string.IsNullOrEmpty(request.Name)
		)
			return new Result
			{
				Status = Status.MissingParameters
			};

		try
		{
			await collection.InsertOneAsync(new Role
			{
				Permissions = request.Permissions,
				Description = request.Description,
				Name = request.Name
			});
		}
		catch (MongoWriteException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	[PermissionsRequired(DefaultPermissions.ManageRoles)]
	public override async Task<Result> DeleteRole(GrpcRoleIdentifier request, ServerCallContext context)
	{
		var collection = _database.GetCollection<Role>(_databaseSettings.RolesCollection);

		switch (request.IdentifierCase)
		{
			case GrpcRoleIdentifier.IdentifierOneofCase.Id:
				await collection.DeleteOneAsync(r => r.Id == request.Id);
				break;
			case GrpcRoleIdentifier.IdentifierOneofCase.Name:
				await collection.DeleteOneAsync(r => r.Name == request.Name);
				break;
			case GrpcRoleIdentifier.IdentifierOneofCase.None:
			default:
				return new Result
				{
					Status = Status.MissingParameters
				};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	[PermissionsRequired(DefaultPermissions.ManageRoles)]
	public override async Task<Result> ModifyRole(GrpcRole request, ServerCallContext context)
	{
		var collection = _database.GetCollection<Role>(_databaseSettings.RolesCollection);

		if (!request.HasId)
			return new Result
			{
				Status = Status.MissingParameters
			};

		var update = Builders<Role>.Update;
		var updates = new List<UpdateDefinition<Role>>();
		var filter = Builders<Role>.Filter.Eq(r => r.Id, request.Id);

		if (!string.IsNullOrEmpty(request.Description))
			updates.Add(update.Set(r => r.Description, request.Description));

		if (!string.IsNullOrEmpty(request.Name))
			updates.Add(update.Set(r => r.Name, request.Name));

		if (request.HasPermissions) updates.Add(update.Set(r => r.Permissions, request.Permissions));

		try
		{
			if (updates.Any()) await collection.UpdateOneAsync(filter, update.Combine(updates));
		}
		catch (MongoException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}


		return new Result
		{
			Status = Status.Ok
		};
	}

	[AuthenticationRequired]
	public override async Task<GrpcRoleResult> FetchRole(GrpcRoleIdentifier request, ServerCallContext context)
	{
		var collection = _database.GetCollection<Role>(_databaseSettings.RolesCollection);
		Role result;

		switch (request.IdentifierCase)
		{
			case GrpcRoleIdentifier.IdentifierOneofCase.Id:
				result = await collection.Find(r => r.Id == request.Id).FirstOrDefaultAsync();
				break;
			case GrpcRoleIdentifier.IdentifierOneofCase.Name:
				result = await collection.Find(r => r.Name == request.Name).FirstOrDefaultAsync();
				break;
			case GrpcRoleIdentifier.IdentifierOneofCase.None:
			default:
				return new GrpcRoleResult
				{
					Status = new Result
					{
						Status = Status.MissingParameters
					}
				};
		}

		if (result == null)
			return new GrpcRoleResult
			{
				Status = new Result
				{
					Status = Status.Error,
					Error = "User not exists"
				}
			};

		var grpcRole = new GrpcRole
		{
			Id = result.Id,
			Permissions = result.Permissions,
			Description = result.Description,
			Name = result.Name
		};

		return new GrpcRoleResult
		{
			Role = grpcRole,
			Status = new Result
			{
				Status = Status.Ok
			}
		};
	}

	[AuthenticationRequired]
	public override async Task<GrpcRoleResults> FetchAllRoles(Empty request, ServerCallContext context)
	{
		var collection = _database.GetCollection<Role>(_databaseSettings.RolesCollection);

		var result = await collection.Find(_ => true).ToListAsync();
		var grpcRoles = new GrpcRole[result.Count];

		if (result.Count == 0)
			return new GrpcRoleResults
			{
				Status = new Result
				{
					Status = Status.Error,
					Error = "No role found"
				}
			};

		for (var i = 0; i < result.Count; i++)
			grpcRoles[i] = new GrpcRole
			{
				Id = result[i].Id,
				Permissions = result[i].Permissions,
				Description = result[i].Description,
				Name = result[i].Name
			};

		return new GrpcRoleResults
		{
			Roles = { grpcRoles },
			Status = new Result
			{
				Status = Status.Ok
			}
		};
	}
}