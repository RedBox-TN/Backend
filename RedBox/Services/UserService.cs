using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Email_utility;
using RedBox.Encryption_utility;
using RedBox.Settings;
using RedBoxAuth;
using RedBoxAuth.Authorization;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

//[AuthenticationRequired]
public partial class UserService : GrpcUserServices.GrpcUserServicesBase
{
    private readonly IMongoDatabase _database;
    private readonly IEmailUtility _emailUtility;
    private readonly IEncryptionUtility _encryptionUtility;
    private readonly IPasswordUtility _passwordUtility;
    private readonly RedBoxSettings _redBoxSettings;
    private readonly AccountDatabaseSettings _settings;
    private readonly ITotpUtility _totpUtility;

    public UserService(IOptions<AccountDatabaseSettings> options, IPasswordUtility passwordUtility,
        ITotpUtility totpUtility
        , IEmailUtility emailUtility, IEncryptionUtility encryptionUtility, IOptions<RedBoxSettings> redboxSettings)
    {
        _settings = options.Value;
        var mongodbClient = new MongoClient(options.Value.ConnectionString);
        _database = mongodbClient.GetDatabase(options.Value.DatabaseName);
        _passwordUtility = passwordUtility;
        _totpUtility = totpUtility;
        _emailUtility = emailUtility;
        _encryptionUtility = encryptionUtility;
        _redBoxSettings = redboxSettings.Value;
    }

    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")]
    private static partial Regex MyRegex();

    /// <summary>
    ///     API for user creation
    /// </summary>
    /// <param name="request">data from the client</param>
    /// <param name="context">current context</param>
    /// <returns>Status code and message of the operation</returns>
    // TODO must add pre-existing chats to user during creation through chat creation API
    //[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
    public override async Task<Result> CreateUser(GrpcUser request, ServerCallContext context)
    {
        var password = _passwordUtility.GeneratePassword();
        var salt = _passwordUtility.CreateSalt();
        var passwordHash = _passwordUtility.HashPassword(password, salt);

        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        // check if data is empty and if email is an email
        if (
            string.IsNullOrEmpty(request.Name) ||
            string.IsNullOrEmpty(request.Surname) ||
            string.IsNullOrEmpty(request.Username) ||
            !MyRegex().IsMatch(request.Email) ||
            string.IsNullOrEmpty(request.Roleid)
        )
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong format for one of the values or missing value"
            };

        // creates the new user in the database TODO add FASeed
        try
        {
            await collection.InsertOneAsync(new User
            {
                Name = request.Name,
                Surname = request.Surname,
                Username = request.Username,
                Email = request.Email,
                RoleId = request.Roleid,
                IsFaEnable = request.Isfaenabled,
                PasswordHash = passwordHash,
                Salt = salt,
                PasswordHistory = new List<byte[]> { passwordHash },
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
            await _emailUtility.SendAccountCreationAsync(
                request.Email,
                request.Username,
                request.Name,
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
    //[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
    public override async Task<Result> DeleteUser(GrpcUser request, ServerCallContext context)
    {
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        if (!request.HasId)
            return new Result
            {
                Status = Status.Error,
                Error = ""
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
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        // if user has permission to modify accounts
        if (AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts) && request.HasId)
        {
            var update = Builders<User>.Update;
            var updates = new List<UpdateDefinition<User>>();
            var filter = Builders<User>.Filter.Eq(user1 => user1.Id, request.Id);

            // Name modification
            if (!string.IsNullOrEmpty(request.Name)) updates.Add(update.Set(user1 => user1.Name, request.Name));

            // Surname modification
            if (!string.IsNullOrEmpty(request.Surname))
                updates.Add(update.Set(user1 => user1.Surname, request.Surname));

            // Username modification
            if (!string.IsNullOrEmpty(request.Username))
                updates.Add(update.Set(user1 => user1.Username, request.Username));

            // Email modification, passing through email mod API
            if (MyRegex().IsMatch(request.Email)) await InitEmailChange(request);

            // RoleID modification, assume roleID is correct
            if (!string.IsNullOrEmpty(request.Roleid)) updates.Add(update.Set(user1 => user1.RoleId, request.Roleid));

            // Path to profile image modification
            if (!string.IsNullOrEmpty(request.Pathtopic))
                updates.Add(update.Set(user1 => user1.PathToPic, request.Pathtopic));

            // FA enabling or disabling
            if (request.HasIsfaenabled) await FAStateChange(request, context);

            // Block / Unblock account
            if (request.HasIsblocked) updates.Add(update.Set(user1 => user1.IsBlocked, request.Isblocked));

            // Biography modification, can only change own bio
            if (!string.IsNullOrEmpty(request.Biography) /*&& user.Id == request.Id*/
               ) updates.Add(update.Set(user1 => user1.Biography, request.Biography));

            // Chats modification, expects new full set of chats every time
            if (request.Chats.Any())
                updates.Add(update.Set<string[]>(user1 => user1.ChatsCollectionNames, request.Chats.ToArray()));

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
        }
        else if (!request.HasId) // if user is trying to modify his own account
        {
            var update = Builders<User>.Update;
            var updates = new List<UpdateDefinition<User>>();
            var filter = Builders<User>.Filter.Eq(user1 => user1.Id, user.Id);

            // Email modification, passing through email mod API
            if (MyRegex().IsMatch(request.Email)) await InitEmailChange(request);

            // Path to profile pic modification
            if (!string.IsNullOrEmpty(request.Pathtopic))
                updates.Add(update.Set(user1 => user1.PathToPic, request.Pathtopic));

            // Biography modification
            if (!string.IsNullOrEmpty(request.Biography))
                updates.Add(update.Set(user1 => user1.Biography, request.Biography));

            // Chats modification, expects new full set of chats every time
            if (request.Chats.Any())
                updates.Add(update.Set<string[]>(user1 => user1.ChatsCollectionNames, request.Chats.ToArray()));

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
    ///     Function to Init email change, sends a message to the new email to confirm it
    /// </summary>
    /// <param name="request">User info from the calling function</param>
    private async Task InitEmailChange(GrpcUser request)
    {
        await _emailUtility.SendEmailChangedAsync(request.Email, request.Id);
    }

    /// <summary>
    ///     API to check if the token hasn't expired yet (Token verification To Be Implemented)
    /// </summary>
    /// <param name="request">Request containing only the AES token</param>
    /// <param name="context">current Context</param>
    /// <returns>Status code and message of the operation</returns>
    public override async Task<Result> TokenCheck(GrpcToken request, ServerCallContext context)
    {
        // Retrieve token and convert to bytes
        var byteToken = HttpUtility.UrlDecodeToBytes(request.Token);

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
        var key = _encryptionUtility.DeriveKey(_redBoxSettings.PasswordResetKey, _redBoxSettings.AesKeySize);

        byte[] plainText;
        try
        {
            plainText = await _encryptionUtility.DecryptAsync(ciphertext, key, iv, _redBoxSettings.AesKeySize);
        }
        catch (Exception)
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };
        }

        // Split data by separator '#'
        var splitData = Encoding.UTF8.GetString(plainText).Split("#");

        if (splitData.Length < 2)
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };

        // Extract expiration time from string
        if (!DateTime.TryParseExact(splitData[1], "ddMMyyyyHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var expiration))
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };

        // Check if token is expired
        if (DateTime.Compare(expiration, DateTime.Now) < 0)
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
    public override async Task<Result> FinalizeEmailChange(GrpcToken request, ServerCallContext context)
    {
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        // Retrieve token and convert ot bytes
        var byteToken = HttpUtility.UrlDecodeToBytes(request.Token);

        if (byteToken.Length < 17)
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };

        // Retrieve IV and Ciphertext, derive AES key
        var iv = byteToken.Take(16).ToArray();
        var ciphertext = byteToken.Skip(16).ToArray();
        var key = _encryptionUtility.DeriveKey(_redBoxSettings.PasswordResetKey, _redBoxSettings.AesKeySize);

        byte[] plainText;
        try
        {
            plainText = await _encryptionUtility.DecryptAsync(ciphertext, key, iv, _redBoxSettings.AesKeySize);
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
        if (!MyRegex().IsMatch(splitData[1]))
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };

        var filter = Builders<User>.Filter.Eq(user => user.Id, splitData[0]);
        var update = Builders<User>.Update.Set(user => user.Email, splitData[1]);

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
    ///     API to modify a user's password
    /// </summary>
    /// <param name="request">Request containing AES token, old and new passowrd</param>
    /// <param name="context">current Context</param>
    /// <returns>Status code and message of the operation</returns>
    public override async Task<Result> ModifyPassword(GrpcPasswordMod request, ServerCallContext context)
    {
        // Retrieve token and convert to bytes
        var byteToken = HttpUtility.UrlDecodeToBytes(request.Token);
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

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
        var key = _encryptionUtility.DeriveKey(_redBoxSettings.PasswordResetKey, _redBoxSettings.AesKeySize);

        byte[] plainText;
        try
        {
            plainText = await _encryptionUtility.DecryptAsync(ciphertext, key, iv, _redBoxSettings.AesKeySize);
        }
        catch (Exception)
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };
        }

        // Split data by separator '#'
        var splitData = Encoding.UTF8.GetString(plainText).Split("#");

        User result;
        var id = splitData[0];
        var filter = Builders<User>.Filter.Eq(user => user.Id, id);

        // Fetch user by ID
        try
        {
            result = await collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (MongoQueryException e)
        {
            return new Result
            {
                Status = Status.Error,
                Error = e.Message
            };
        }

        // Check if old password coincides with saved password
        if (result.PasswordHash != _passwordUtility.HashPassword(request.Password, result.Salt))
            return new Result
            {
                Status = Status.Error,
                Error = "Old password is wrong"
            };

        var passwordHistory = result.PasswordHistory;
        var newPasswordHash = _passwordUtility.HashPassword(request.Newpassword, result.Salt);

        // Check if new password is new enough (check if is in pw history)
        if (passwordHistory.Any(pw => pw == newPasswordHash))
            return new Result
            {
                Status = Status.Error,
                Error = "New password has already been used"
            };

        //TODO number 3 could become setting
        // Rebuild password history with only last 3 passwords (including the one added in this function)
        passwordHistory.Insert(0, newPasswordHash);
        if (passwordHistory.Count > 3) passwordHistory = passwordHistory.GetRange(0, 3);
        var update = Builders<User>.Update.Set(user => user.PasswordHash, newPasswordHash)
            .Set(user => user.PasswordHistory, passwordHistory);

        // Update db with new password hash and history
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
    [PermissionsRequired(DefaultPermissions.ResetUsersPassword)]
    public override async Task<Result> PasswordReset(GrpcUser request, ServerCallContext context)
    {
        string password;
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        // check if data is empty
        if (!request.HasId)
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong format for one of the values or missing value"
            };

        User result;
        var filter = Builders<User>.Filter.Eq(user => user.Id, request.Id);

        // Fetch user from db by ID
        try
        {
            result = await collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (MongoQueryException e)
        {
            return new Result
            {
                Status = Status.Error,
                Error = e.Message
            };
        }

        var passwordHistory = result.PasswordHistory;
        byte[] passwordHash;

        // Keep generating new passwords until they are not in the history
        do
        {
            password = _passwordUtility.GeneratePassword();
            passwordHash = _passwordUtility.HashPassword(password, result.Salt);
        } while (passwordHistory.Any(pw => pw == passwordHash));

        //TODO number 3 could become setting
        // Rebuild password history with only last 3 passwords (including the one added in this function)
        passwordHistory.Insert(0, passwordHash);
        if (passwordHistory.Count > 3) passwordHistory = passwordHistory.GetRange(0, 3);
        var update = Builders<User>.Update.Set(user => user.PasswordHash, passwordHash)
            .Set(user => user.PasswordHistory, passwordHistory);

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

        // sends an email to the new user with the first-time password
        try
        {
            await _emailUtility.SendNewPasswordAsync(
                result.Email,
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
    ///     API to enable/disable 2 factor authentication only for users
    /// </summary>
    /// <param name="request">user with ID and new 2FA value</param>
    /// <param name="context">current context</param>
    /// <returns>Status code and message of operation, qrcode and manual code for 2FA</returns>
    public override async Task<Grpc2faResult> FAStateChange(GrpcUser request, ServerCallContext context)
    {
        var collection = _database.GetCollection<User>(_settings.UsersCollection);
        var user = context.GetUser();

        // missing informations
        if (!request.HasIsfaenabled || !request.HasId || !MyRegex().IsMatch(request.Email))
            return new Grpc2faResult
            {
                Status = new Result
                {
                    Status = Status.Error,
                    Error = "Needed information not provided"
                }
            };

        var filter = Builders<User>.Filter.Eq(user1 => user1.Id, request.Id);
        var update = Builders<User>.Update.Set(user1 => user1.IsFaEnable, request.Isfaenabled);
        string? qrcode = null, manualCode = null;

        switch (request.HasId)
        {
            case false when AuthorizationMiddleware.HasPermission(user, DefaultPermissions.EnableLocal2Fa):
            {
                if (request.Isfaenabled)
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
                if (!request.Isfaenabled) update.Set(user1 => user1.FaSeed, null);

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
            Manualcode = manualCode,
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
    public override async Task<GrpcProvisionResult> AccountProvision(GrpcUser request, ServerCallContext context)
    {
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        if (!request.HasId)
            return new GrpcProvisionResult
            {
                Status = new Result
                {
                    Status = Status.Error,
                    Error = "ID not specified"
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
            Faprovisioning = faProvisioning,
            Passwordprovisioning = passwordProvisioning,
            Keyprovisioning = keyProvisioning,
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
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        if (!request.HasIsblocked || !request.HasId)
            return new Result
            {
                Status = Status.Error,
                Error = "Needed information not provided"
            };

        var filter = Builders<User>.Filter.Eq(user => user.Id, request.Id);
        var update = Builders<User>.Update.Set(user => user.IsBlocked, request.Isblocked);

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
    ///     API to fetch a specific user
    /// </summary>
    /// <param name="request">user ID</param>
    /// <param name="context">current context</param>
    /// <returns>contains a user with all the necessary data and a status code</returns>
    public override async Task<GrpcUserResult> FetchUser(GrpcUser request, ServerCallContext context)
    {
        var collection = _database.GetCollection<User>(_settings.UsersCollection);
        User result;

        if (!request.HasId)
            return new GrpcUserResult
            {
                Status = new Result
                {
                    Status = Status.Error,
                    Error = "ID not sent"
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
                Roleid = string.IsNullOrEmpty(result.RoleId) ? "" : result.RoleId,
                Isblocked = result.IsBlocked,
                Isfaenabled = result.IsFaEnable,
                Username = string.IsNullOrEmpty(result.Username) ? "" : result.Username,
                Biography = string.IsNullOrEmpty(result.Biography) ? "" : result.Biography,
                Pathtopic = string.IsNullOrEmpty(result.PathToPic) ? "" : result.PathToPic,
                Chats = { result.ChatsCollectionNames ?? new[] { "" } }
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
    public override async Task<GrpcUserResult> FetchAllUsers(Empty request, ServerCallContext context)
    {
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

        var result = await collection.FindSync(_ => true).ToListAsync();
        var users = new GrpcUser[result.Count];

        for (var i = 0; i < result.Count; i++)
            users[i] = new GrpcUser
            {
                Id = string.IsNullOrEmpty(result[i].Id) ? "" : result[i].Id,
                Name = string.IsNullOrEmpty(result[i].Name) ? "" : result[i].Name,
                Surname = string.IsNullOrEmpty(result[i].Surname) ? "" : result[i].Surname,
                Email = string.IsNullOrEmpty(result[i].Email) ? "" : result[i].Email,
                Roleid = string.IsNullOrEmpty(result[i].RoleId) ? "" : result[i].RoleId,
                Isblocked = result[i].IsBlocked,
                Isfaenabled = result[i].IsFaEnable,
                Username = string.IsNullOrEmpty(result[i].Username) ? "" : result[i].Username,
                Biography = string.IsNullOrEmpty(result[i].Biography) ? "" : result[i].Biography,
                Pathtopic = string.IsNullOrEmpty(result[i].PathToPic) ? "" : result[i].PathToPic,
                Chats = { result[i].ChatsCollectionNames ?? new[] { "" } }
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
}