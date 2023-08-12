using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
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
    private readonly AccountDatabaseSettings _settings;
    private readonly RedBoxSettings _redBoxSettings;
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

        // creates the new user in the database
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
                Biography = "Business account"
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
                Error = "No ID provided"
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
        if (AuthorizationMiddleware.HasPermission(user, DefaultPermissions.ManageUsersAccounts))
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
            if (MyRegex().IsMatch(request.Email))
            {
                await InitEmailChange(request);
            }

            // ReoleID modification, assume roleID is correct
            if (!string.IsNullOrEmpty(request.Roleid)) updates.Add(update.Set(user1 => user1.RoleId, request.Roleid));

            // Path to profile image modification
            if (!string.IsNullOrEmpty(request.Pathtopic))
                updates.Add(update.Set(user1 => user1.PathToPic, request.Pathtopic));

            // FA enabling or disabling
            if (request.HasIsfaenabled) updates.Add(update.Set(user1 => user1.IsFaEnable, request.Isfaenabled));

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
        else if (user.Id == request.Id) // if user is trying to modify his own account
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
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token (token length)"
            };
        }
        
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
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };
        }
        
        // Extract expiration time from string
        if (!DateTime.TryParseExact(splitData[1], "ddMMyyyyHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration))
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };
        }

        // Check if token is expired
        if (DateTime.Compare(expiration, DateTime.Now) < 0)
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Token Expired"
            };
        }
        
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
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            };
        }
        
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
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong token"
            }; 
        }

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
    //TODO WIP Method!
    public override async Task<Result> ModifyPassword(GrpcPasswordMod request, ServerCallContext context)
    {
        return new Result
        {
            Status = Status.Ok
        };
    }
}