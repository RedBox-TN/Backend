using System.Text.RegularExpressions;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Email_utility;
using RedBox.Permission_Utility;
using RedBoxAuth.Authorization;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Settings;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

public partial class AdminService : GrpcAdminServices.GrpcAdminServicesBase
{
	private readonly IMongoDatabase _database;
	private readonly AccountDatabaseSettings _databaseSettings;
	private readonly IPasswordUtility _passwordUtility;
	private readonly IPermissionUtility _permissionUtility;
	private readonly IRedBoxEmailUtility _redBoxEmailUtility;


	public AdminService(IOptions<AccountDatabaseSettings> databaseSettings, IPasswordUtility passwordUtility,
		IRedBoxEmailUtility redBoxEmailUtility, IPermissionUtility permissionUtility)
	{
		_databaseSettings = databaseSettings.Value;
		var mongodbClient = new MongoClient(_databaseSettings.UsersCollection);
		_database = mongodbClient.GetDatabase(_databaseSettings.DatabaseName);
		_passwordUtility = passwordUtility;
		_redBoxEmailUtility = redBoxEmailUtility;
		_permissionUtility = permissionUtility;
	}

	[GeneratedRegex(@"^[\w-.]+@([\w-]+\.)+[\w-]{2,4}$")]
	private static partial Regex MyRegex();

	/// <summary>
	///     API for user creation
	/// </summary>
	/// <param name="request">data from the client</param>
	/// <param name="context">current context</param>
	/// <returns>Status code and message of the operation</returns>
	[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
	public override async Task<Result> CreateUser(GrpcUser request, ServerCallContext context)
	{
		var password = _passwordUtility.GeneratePassword();
		var salt = _passwordUtility.CreateSalt();
		var passwordHash = _passwordUtility.HashPassword(password, salt);

		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		// check if data is empty and if email is an email
		if (
			string.IsNullOrEmpty(request.Name) ||
			string.IsNullOrEmpty(request.Surname) ||
			string.IsNullOrEmpty(request.Username) ||
			!MyRegex().IsMatch(request.Email) ||
			string.IsNullOrEmpty(request.RoleId)
		)
			return new Result
			{
				Status = Status.MissingParameters,
				Error = "Wrong format for one of the values or missing value"
			};

		// creates the new user in the database
		try
		{
			await collection.InsertOneAsync(new User
			{
				Name = request.Name.Normalize(),
				Surname = request.Surname.Normalize(),
				Username = request.Username.Normalize(),
				Email = request.Email.Normalize(),
				RoleId = request.RoleId,
				IsFaEnable = request.IsFaEnabled,
				PasswordHash = passwordHash,
				Salt = salt,
				PasswordHistory = new List<(byte[] Password, byte[] Salt)>
				{
					(Password: passwordHash, Salt: salt)
				},
				Biography = "Business account",
				NeedsProvisioning = true,
				ChatIds = Array.Empty<string>(),
				GroupIds = Array.Empty<string>()
			});
		}
		catch (Exception e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		// sends an email to the new user with the first-time password
		try
		{
			await _redBoxEmailUtility.SendAccountCreationAsync(
				request.Email.Normalize(),
				request.Username.Normalize(),
				request.Name.Normalize(),
				password
			);
		}
		catch (Exception e)
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

	/// <summary>
	///     API for the removal of an account
	/// </summary>
	/// <param name="request">data from the client</param>
	/// <param name="context">current context</param>
	/// <returns>Status code and message of the operation</returns>
	[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
	public override async Task<Result> DeleteUser(GrpcUser request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		if (!request.HasId)
			return new Result
			{
				Status = Status.MissingParameters
			};

		try
		{
			await collection.DeleteOneAsync(user => user.Id == request.Id);
		}
		catch (Exception e)
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

	/// <summary>
	///     API for the modification of an account
	/// </summary>
	/// <param name="request">data from the client</param>
	/// <param name="context">current context</param>
	/// <returns>Status code and message of the operation</returns>
	[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
	public override async Task<Result> AdminModifyUser(GrpcUser request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		if (!request.HasId)
			return new Result
			{
				Status = Status.MissingParameters
			};

		var update = Builders<User>.Update;
		var updates = new List<UpdateDefinition<User>>();
		var filter = Builders<User>.Filter.Eq(user1 => user1.Id, request.Id);

		// Name modification
		if (!string.IsNullOrEmpty(request.Name))
			updates.Add(update.Set(user1 => user1.Name, request.Name.Normalize()));

		// Surname modification
		if (!string.IsNullOrEmpty(request.Surname))
			updates.Add(update.Set(user1 => user1.Surname, request.Surname.Normalize()));

		// Email modification, passing through email mod API
		if (MyRegex().IsMatch(request.Email))
		{
			var username = collection.Find(filter).First().Username.Normalize();
			await _redBoxEmailUtility.SendEmailChangedAsync(request.Email.Normalize(), request.Id, username);
		}

		// RoleId modification, assume RoleId is correct
		if (!string.IsNullOrEmpty(request.RoleId)) updates.Add(update.Set(user1 => user1.RoleId, request.RoleId));

		// FA enabling or disabling
		if (request.HasIsFaEnabled)
		{
			updates.Add(update.Set(u => u.IsFaEnable, request.IsFaEnabled));
			if (!request.IsFaEnabled) update.Set(user1 => user1.FaSeed, null);
		}

		// Combination of all modifications, only if list is not empty
		try
		{
			if (updates.Any()) await collection.UpdateOneAsync(filter, update.Combine(updates));
		}
		catch (Exception e)
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

	/// <summary>
	///     API to block/unblock users
	/// </summary>
	/// <param name="request">user with ID and new block value</param>
	/// <param name="context">current context</param>
	/// <returns>Status code and message of operation</returns>
	[PermissionsRequired(DefaultPermissions.BlockUsersLogin)]
	public override async Task<Result> BlockStateChange(GrpcUser request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		if (!request.HasIsBlocked || !request.HasId)
			return new Result
			{
				Status = Status.MissingParameters
			};

		var filter = Builders<User>.Filter.Eq(user => user.Id, request.Id);
		var update = Builders<User>.Update.Set(user => user.IsBlocked, request.IsBlocked);

		try
		{
			await collection.UpdateOneAsync(filter, update);
			if (request.IsBlocked)
			{
				var user = collection.Find(filter).First();
				await _redBoxEmailUtility.SendAccountLockNotificationAsync(user.Email, user.Username);
			}
		}
		catch (Exception e)
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

	/// <summary>
	///     API to generate new password and send it via email
	/// </summary>
	/// <param name="request">user containing email and ID</param>
	/// <param name="context">current Context</param>
	/// <returns>Status code and message of the operation</returns>
	[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
	public override async Task<Result> SetUserRandomPassword(StringMessage request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		// check if data is empty
		if (string.IsNullOrEmpty(request.Value))
			return new Result
			{
				Status = Status.MissingParameters
			};

		User user;
		var filter = Builders<User>.Filter.Eq(u => u.Id, request.Value);

		// Fetch user from db by ID
		try
		{
			user = await collection.Find(filter).FirstOrDefaultAsync();
		}
		catch (MongoQueryException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		var salt = _passwordUtility.CreateSalt();
		var password = _passwordUtility.GeneratePassword();
		var passwordHash = _passwordUtility.HashPassword(password, salt);

		var update = Builders<User>.Update.Set(u => u.PasswordHash, passwordHash).Set(u => u.Salt, salt)
			.Set(u => u.NeedsProvisioning, true);

		// Update password hash and history
		try
		{
			await collection.UpdateOneAsync(filter, update);
		}
		catch (Exception e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		await _redBoxEmailUtility.SendAdminPasswordChangedAsync(user.Email, password, user.Username);

		return new Result
		{
			Status = Status.Ok
		};
	}
}