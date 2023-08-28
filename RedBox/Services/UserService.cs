using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Email_utility;
using RedBox.Settings;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using RedBoxServices;
using Shared;
using Shared.Models;
using Shared.Settings;
using Shared.Utility;
using Status = Shared.Status;

namespace RedBox.Services;

public partial class UserService : GrpcUserServices.GrpcUserServicesBase
{
	private readonly CommonEmailSettings _commonEmailSettings;
	private readonly IMongoDatabase _database;
	private readonly AccountDatabaseSettings _databaseSettings;
	private readonly IEncryptionUtility _encryptionUtility;
	private readonly IPasswordUtility _passwordUtility;
	private readonly IRedBoxEmailUtility _redBoxEmailUtility;
	private readonly RedBoxSettings _redBoxSettings;
	private readonly ITotpUtility _totpUtility;

	public UserService(IOptions<AccountDatabaseSettings> options, IPasswordUtility passwordUtility,
		ITotpUtility totpUtility, IRedBoxEmailUtility redBoxEmailUtility, IEncryptionUtility encryptionUtility,
		IOptions<CommonEmailSettings> commonEmailSettings, IOptions<RedBoxSettings> redBoxSettings)
	{
		_databaseSettings = options.Value;
		var mongodbClient = new MongoClient(options.Value.ConnectionString);
		_database = mongodbClient.GetDatabase(options.Value.DatabaseName);
		_passwordUtility = passwordUtility;
		_totpUtility = totpUtility;
		_redBoxEmailUtility = redBoxEmailUtility;
		_encryptionUtility = encryptionUtility;
		_redBoxSettings = redBoxSettings.Value;
		_commonEmailSettings = commonEmailSettings.Value;
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
				ChatIds = request.Chats.ToArray(),
				IsFaEnable = request.IsFaEnabled,
				PasswordHash = passwordHash,
				Salt = salt,
				PasswordHistory = new List<(byte[] Password, byte[] Salt)> { (Password: passwordHash, Salt: salt) },
				Biography = "Business account",
				NeedsProvisioning = true
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
	public override async Task<Result> ModifyUser(GrpcUser request, ServerCallContext context)
	{
		var user = context.GetUser();
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		// if user has permission to modify accounts
		if (AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts) && request.HasId)
		{
			var update = Builders<User>.Update;
			var updates = new List<UpdateDefinition<User>>();
			var filter = Builders<User>.Filter.Eq(user1 => user1.Id, request.Id);

			// Name modification
			if (!string.IsNullOrEmpty(request.Name))
				updates.Add(update.Set(user1 => user1.Name, request.Name.Normalize()));

			// Surname modification
			if (!string.IsNullOrEmpty(request.Surname))
				updates.Add(update.Set(user1 => user1.Surname, request.Surname.Normalize()));

			//todo si puo' modificare??
			// Username modification
			if (!string.IsNullOrEmpty(request.Username))
				updates.Add(update.Set(user1 => user1.Username, request.Username));

			// Email modification, passing through email mod API
			if (MyRegex().IsMatch(request.Email))
			{
				var username = collection.Find(filter).First().Username.Normalize();
				await _redBoxEmailUtility.SendEmailChangedAsync(request.Email.Normalize(), request.Id, username);
			}

			// RoleId modification, assume RoleId is correct
			if (!string.IsNullOrEmpty(request.RoleId)) updates.Add(update.Set(user1 => user1.RoleId, request.RoleId));

			// Path to profile image modification
			if (!string.IsNullOrEmpty(request.PathToPic))
				updates.Add(update.Set(user1 => user1.PathToPic, request.PathToPic));

			// FA enabling or disabling
			if (request.HasIsFaEnabled) await FAStateChange(request, context);

			// Block / Unblock account
			if (request.HasIsBlocked) updates.Add(update.Set(user1 => user1.IsBlocked, request.IsBlocked));

			// Biography modification, can only change own bio
			if (!string.IsNullOrEmpty(request.Biography) /*&& user.Id == request.Id*/
			   ) updates.Add(update.Set(user1 => user1.Biography, request.Biography));

			// if chats has elements set new chats directly
			if (request.Chats.Any())
			{
				updates.Add(update.Set<string[]?>(user1 => user1.ChatIds, request.Chats.ToArray()));
			}
			else
			{
				// remove elements from chats
				if (request.RemovedChats.Any())
					updates.Add(update.PullAll(user1 => user1.ChatIds, request.RemovedChats));

				// add new elements to chats
				if (request.AddedChats.Any())
					updates.Add(update.AddToSetEach(user1 => user1.ChatIds, request.AddedChats));
			}

			// Combination of all modifications, only if list is not empty
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
		}
		else if (!request.HasId) // if user is trying to modify his own account
		{
			var update = Builders<User>.Update;
			var updates = new List<UpdateDefinition<User>>();
			var filter = Builders<User>.Filter.Eq(user1 => user1.Id, user.Id);

			// Email modification, passing through email mod API
			if (MyRegex().IsMatch(request.Email))
				await _redBoxEmailUtility.SendEmailChangedAsync(request.Email.Normalize(), request.Id,
					user.Username.Normalize());

			// Path to profile pic modification
			if (!string.IsNullOrEmpty(request.PathToPic))
				updates.Add(update.Set(user1 => user1.PathToPic, request.PathToPic));

			// Biography modification
			if (!string.IsNullOrEmpty(request.Biography))
				updates.Add(update.Set(user1 => user1.Biography, request.Biography));

			// if chats has elements set new chats directly
			if (request.Chats.Any())
			{
				updates.Add(update.Set<string[]?>(user1 => user1.ChatIds, request.Chats.ToArray()));
			}
			else
			{
				// remove elements from chats
				if (request.RemovedChats.Any())
					updates.Add(update.PullAll(user1 => user1.ChatIds, request.RemovedChats));

				// add new elements to chats
				if (request.AddedChats.Any())
					updates.Add(update.AddToSetEach(user1 => user1.ChatIds, request.AddedChats));
			}

			// Combination of all modifications, only if list is not empty
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
		}
		else
		{
			return new Result
			{
				Status = Status.Error,
				Error = "You don't have the necessary permissions to perform this action."
			};
		}

		return new Result
		{
			Status = Status.Ok
		};
	}

	/// <summary>
	///     API to check if the token hasn't expired yet (Token verification To Be Implemented)
	/// </summary>
	/// <param name="request">Request containing only the encrypted token</param>
	/// <param name="context">current Context</param>
	/// <returns>Status code and message of the operation</returns>
	public override async Task<Result> TokenCheck(StringRequest request, ServerCallContext context)
	{
		// Retrieve token and convert to bytes
		var byteToken = HttpUtility.UrlDecodeToBytes(request.Value);

		// Token too short (IV minimum 16 bytes)
		if (byteToken.Length < 17)
			return new Result
			{
				Status = Status.Error,
				Error = "Wrong token (token length)"
			};

		// Retrieve IV and Ciphertext, derive AES key
		var iv = byteToken.Take(16).ToArray();
		var ciphertext = byteToken.Skip(16).ToArray();
		var key = _encryptionUtility.DeriveKey(_commonEmailSettings.TokenEncryptionKey);

		byte[] plainText;
		try
		{
			plainText = await _encryptionUtility.AesDecryptAsync(ciphertext, key, iv);
		}
		catch (Exception)
		{
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};
		}

		// Split data by separator '#'
		var splitData = Encoding.UTF8.GetString(plainText).Split("#");

		if (splitData.Length is < 2 or > 3)
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};

		// Extract expiration time from string
		if (!long.TryParse(splitData[^1], out var expiration))
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};

		// Check if token is expired
		if (DateTimeOffset.Now.ToUnixTimeMilliseconds() >= expiration)
			return new Result
			{
				Status = Status.Error,
				Error = "Token Expired"
			};

		return new Result
		{
			Status = Status.Ok
		};
	}

	/// <summary>
	///     API to finalize the email change of an account, receives token which contains ID and Email
	/// </summary>
	/// <param name="request">Request containing only the AES token</param>
	/// <param name="context">current Context</param>
	/// <returns>Status code and message of the operation</returns>
	public override async Task<Result> FinalizeEmailChange(StringRequest request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		// Retrieve token and convert ot bytes
		var byteToken = HttpUtility.UrlDecodeToBytes(request.Value);

		if (byteToken.Length < 17)
			return new Result
			{
				Status = Status.Error,
				Error = "Wrong token"
			};

		// Retrieve IV and Ciphertext, derive AES key
		var iv = byteToken.Take(16).ToArray();
		var ciphertext = byteToken.Skip(16).ToArray();
		var key = _encryptionUtility.DeriveKey(_commonEmailSettings.TokenEncryptionKey);

		byte[] plainText;
		try
		{
			plainText = await _encryptionUtility.AesDecryptAsync(ciphertext, key, iv);
		}
		catch (Exception)
		{
			return new Result
			{
				Status = Status.Error,
				Error = "Wrong token"
			};
		}

		var splitData = Encoding.UTF8.GetString(plainText).Split("#");

		// Check email validity
		if (!MyRegex().IsMatch(splitData[0]))
			return new Result
			{
				Status = Status.Error,
				Error = "Wrong token"
			};

		// Extract expiration time from string
		if (!long.TryParse(splitData[^1], out var expiration))
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};

		// Check if token is expired
		if (expiration - DateTimeOffset.Now.ToUnixTimeMilliseconds() <= 0)
			return new Result
			{
				Status = Status.Error,
				Error = "Token Expired"
			};

		var filter = Builders<User>.Filter.Eq(user => user.Id, splitData[1]);
		var update = Builders<User>.Update.Set(user => user.Email, splitData[0]);

		// Try to access user by ID
		try
		{
			await collection.UpdateOneAsync(filter, update);
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

	/// <summary>
	///     API to generate new password and send it via email
	/// </summary>
	/// <param name="request">user containing email and ID</param>
	/// <param name="context">current Context</param>
	/// <returns>Status code and message of the operation</returns>
	[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
	public override async Task<Result> SetUserRandomPassword(StringRequest request, ServerCallContext context)
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
		catch (MongoWriteException e)
		{
			return new Result
			{
				Status = Status.Error,
				Error = e.Message
			};
		}

		_redBoxEmailUtility.SendPasswordChangedAsync(user.Email, password, user.Username);

		return new Result
		{
			Status = Status.Ok
		};
	}

	/// <summary>
	///     API to enable/disable 2 factor authentication only for users
	/// </summary>
	/// <param name="request">user with ID and new 2FA value</param>
	/// <param name="context">current context</param>
	/// <returns>Status code and message of operation, qrcode and manual code for 2FA</returns>
	public override async Task<Grpc2faResult> FAStateChange(GrpcUser request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);
		var user = context.GetUser();

		// missing parameters
		if (!request.HasIsFaEnabled || !request.HasId || !MyRegex().IsMatch(request.Email))
			return new Grpc2faResult
			{
				Status = new Result
				{
					Status = Status.MissingParameters
				}
			};

		var filter = Builders<User>.Filter.Eq(user1 => user1.Id, request.Id);
		var update = Builders<User>.Update.Set(user1 => user1.IsFaEnable, request.IsFaEnabled);
		string? qrcode = null, manualCode = null;

		switch (request.HasId)
		{
			case false when AuthorizationMiddleware.HasPermission(user, DefaultPermissions.EnableLocal2Fa):
			{
				if (request.IsFaEnabled)
				{
					var faSeed = _totpUtility.CreateSharedSecret(request.Email, out qrcode, out manualCode);
					update.Set(user1 => user1.FaSeed, faSeed);
				}
				else
				{
					update.Set(user1 => user1.FaSeed, null);
				}

				break;
			}
			case true when AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts):
			{
				if (!request.IsFaEnabled) update.Set(user1 => user1.FaSeed, null);

				break;
			}
			default:
				return new Grpc2faResult
				{
					Status = new Result
					{
						Status = Status.Error,
						Error = "permission error"
					}
				};
		}

		try
		{
			await collection.UpdateOneAsync(filter, update);
		}
		catch (MongoWriteException e)
		{
			return new Grpc2faResult
			{
				Status = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			};
		}

		return new Grpc2faResult
		{
			Qrcode = qrcode,
			ManualCode = manualCode,
			Status = new Result
			{
				Status = Status.Ok
			}
		};
	}

	/// <summary>
	///     API called every login to check if there is some form of provisioning to do
	/// </summary>
	/// <param name="request">User with ID</param>
	/// <param name="context">current context</param>
	/// <returns>contains 3 boolean values to specify which provisioning to do and a status code</returns>
	[AuthenticationRequired]
	public override async Task<GrpcProvisionResult> AccountProvision(GrpcUser request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		if (!request.HasId)
			return new GrpcProvisionResult
			{
				Status = new Result
				{
					Status = Status.MissingParameters
				}
			};

		bool faProvisioning = false, passwordProvisioning = false, keyProvisioning = false;
		User result;
		var filter = Builders<User>.Filter.Eq(user => user.Id, request.Id);

		// Fetch user
		try
		{
			result = await collection.Find(filter).FirstOrDefaultAsync();
		}
		catch (Exception e)
		{
			return new GrpcProvisionResult
			{
				Status = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			};
		}

		// check if fa is to setup
		if (result.IsFaEnable && result.FaSeed is null) faProvisioning = true;

		// check if needs first provisioning
		if (result.NeedsProvisioning)
		{
			passwordProvisioning = true;
			keyProvisioning = true;
		}

		return new GrpcProvisionResult
		{
			FaProvisioning = faProvisioning,
			PasswordProvisioning = passwordProvisioning,
			KeyProvisioning = keyProvisioning,
			Status = new Result
			{
				Status = Status.Ok
			}
		};
	}

	/// <summary>
	///     API to block/unblock users
	/// </summary>
	/// <param name="request">user with ID and new block value</param>
	/// <param name="context">current context</param>
	/// <returns>Status code and message of operation</returns>
	[PermissionsRequired(DefaultPermissions.BlockUsers)]
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

	/// <summary>
	///     API to fetch a specific user
	/// </summary>
	/// <param name="request">user ID</param>
	/// <param name="context">current context</param>
	/// <returns>contains a user with all the necessary data and a status code</returns>
	[AuthenticationRequired]
	public override async Task<GrpcUserResult> FetchUser(GrpcUser request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);
		User result;

		if (!request.HasId)
			return new GrpcUserResult
			{
				Status = new Result
				{
					Status = Status.MissingParameters
				}
			};

		try
		{
			result = await collection.FindSync(user1 => user1.Id == request.Id).FirstOrDefaultAsync();
		}
		catch (Exception e)
		{
			return new GrpcUserResult
			{
				Status = new Result
				{
					Status = Status.Error,
					Error = e.Message
				}
			};
		}

		if (result == null)
			return new GrpcUserResult
			{
				Status = new Result
				{
					Status = Status.Error,
					Error = "No matches"
				}
			};

		var user = new GrpcUser[]
		{
			new()
			{
				Id = string.IsNullOrEmpty(result.Id) ? "" : result.Id,
				Name = string.IsNullOrEmpty(result.Name) ? "" : result.Name,
				Surname = string.IsNullOrEmpty(result.Surname) ? "" : result.Surname,
				Email = string.IsNullOrEmpty(result.Email) ? "" : result.Email,
				RoleId = string.IsNullOrEmpty(result.RoleId) ? "" : result.RoleId,
				IsBlocked = result.IsBlocked,
				IsFaEnabled = result.IsFaEnable,
				Username = string.IsNullOrEmpty(result.Username) ? "" : result.Username,
				Biography = string.IsNullOrEmpty(result.Biography) ? "" : result.Biography,
				PathToPic = string.IsNullOrEmpty(result.PathToPic) ? "" : result.PathToPic,
				Chats = { result.ChatIds ?? new[] { "" } }
			}
		};

		return new GrpcUserResult
		{
			User = { user },
			Status = new Result
			{
				Status = Status.Ok
			}
		};
	}

	/// <summary>
	///     API to fetch all users
	/// </summary>
	/// <param name="request">empty request</param>
	/// <param name="context">current context</param>
	/// <returns>contains all the users fetched with the necessary info and a status code</returns>
	[AuthenticationRequired]
	public override async Task<GrpcUserResult> FetchAllUsers(Empty request, ServerCallContext context)
	{
		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);

		var result = await collection.FindSync(_ => true).ToListAsync();
		var users = new GrpcUser[result.Count];

		for (var i = 0; i < result.Count; i++)
			users[i] = new GrpcUser
			{
				Id = string.IsNullOrEmpty(result[i].Id) ? "" : result[i].Id,
				Name = string.IsNullOrEmpty(result[i].Name) ? "" : result[i].Name,
				Surname = string.IsNullOrEmpty(result[i].Surname) ? "" : result[i].Surname,
				Email = string.IsNullOrEmpty(result[i].Email) ? "" : result[i].Email,
				RoleId = string.IsNullOrEmpty(result[i].RoleId) ? "" : result[i].RoleId,
				IsBlocked = result[i].IsBlocked,
				IsFaEnabled = result[i].IsFaEnable,
				Username = string.IsNullOrEmpty(result[i].Username) ? "" : result[i].Username,
				Biography = string.IsNullOrEmpty(result[i].Biography) ? "" : result[i].Biography,
				PathToPic = string.IsNullOrEmpty(result[i].PathToPic) ? "" : result[i].PathToPic,
				Chats = { result[i].ChatIds ?? new[] { "" } }
			};

		return new GrpcUserResult
		{
			User = { users },
			Status = new Result
			{
				Status = Status.Ok
			}
		};
	}

	/// <summary>
	///     API to reset password when it is forgotten
	/// </summary>
	/// <param name="request">token request</param>
	/// <param name="context">current context</param>
	/// <returns>result of operation</returns>
	public override async Task<Result> ForgottenPasswordReset(ForgottenPasswordRequest request,
		ServerCallContext context)
	{
		var token = HttpUtility.UrlDecodeToBytes(request.Token);

		if (token.Length < 17)
			return new Result
			{
				Status = Status.Error,
				Error = "Wrong token (token length)"
			};

		var iv = token.Take(16).ToArray();
		var ciphertext = token.Skip(16).ToArray();
		var key = _encryptionUtility.DeriveKey(_commonEmailSettings.TokenEncryptionKey);

		byte[] plainText;
		try
		{
			plainText = await _encryptionUtility.AesDecryptAsync(ciphertext, key, iv);
		}
		catch (Exception)
		{
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};
		}

		var split = Encoding.UTF8.GetString(plainText).Split("#");

		if (split.Length is < 2 or > 3)
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};

		if (!long.TryParse(split[^1], out var expiration))
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};

		if (DateTimeOffset.Now.ToUnixTimeMilliseconds() >= expiration)
			return new Result
			{
				Status = Status.Error,
				Error = "Token Expired"
			};

		var collection = _database.GetCollection<User>(_databaseSettings.UsersCollection);
		var filter = Builders<User>.Filter.Eq(u => u.Id, split[0]);
		var user = await collection.Find(filter).FirstOrDefaultAsync();

		if (user is null)
			return new Result
			{
				Status = Status.Error,
				Error = "Invalid token"
			};

		var currentPassword = (Password: user.PasswordHash, user.Salt);
		if (WasPasswordAlreadyUsed(user.PasswordHistory, currentPassword, request.NewPassword))
			return new Result
			{
				Status = Status.Error,
				Error = "Password has already been used"
			};

		var salt = _passwordUtility.CreateSalt();
		var passwordHash = _passwordUtility.HashPassword(request.NewPassword, salt);

		try
		{
			var update = Builders<User>.Update.Set(u => u.PasswordHash, passwordHash).Set(u => u.Salt, salt)
				.Set(u => u.NeedsProvisioning, true)
				.Push(u => u.PasswordHistory, currentPassword);

			if (user.PasswordHistory!.Count >= _redBoxSettings.PasswordHistorySize)
				await collection.UpdateOneAsync(filter, Builders<User>.Update.PopFirst(u => u.PasswordHistory));

			await collection.UpdateOneAsync(filter, update);
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

	private bool WasPasswordAlreadyUsed(IEnumerable<(byte[] Password, byte[] Salt)>? history,
		(byte[] Password, byte[] Salt) currentPassword, string newPassword)
	{
		return history is not null &&
		       (currentPassword.Password.SequenceEqual(_passwordUtility.HashPassword(newPassword,
			       currentPassword.Salt)) || history.Any(old =>
			       old.Password.SequenceEqual(_passwordUtility.HashPassword(newPassword, old.Salt))));
	}
}