using System.Runtime.InteropServices.JavaScript;
using System.Text.RegularExpressions;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Email_utility;
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
    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")] private static partial Regex MyRegex();
    
    private readonly IMongoDatabase _database;
    private readonly IPasswordUtility _passwordUtility;
    private readonly ITotpUtility _totpUtility;
    private readonly AccountDatabaseSettings _settings;
    private readonly IEmailUtility _emailUtility;

    public UserService(IOptions<AccountDatabaseSettings> options, IPasswordUtility passwordUtility, ITotpUtility totpUtility
    , IEmailUtility emailUtility)
    {
        _settings = options.Value;
        var mongodbClient = new MongoClient(options.Value.ConnectionString);
        _database = mongodbClient.GetDatabase(options.Value.DatabaseName);
        _passwordUtility = passwordUtility;
        _totpUtility = totpUtility;
        _emailUtility = emailUtility;
    }


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
        {
            return new Result
            {
                Status = Status.Error,
                Error = "Wrong format for one of the values or missing value"
            };
        }
        
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
        {
            return new Result
            {
                Status = Status.Error,
                Error = "No ID provided"
            };
        }

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
            if (!string.IsNullOrEmpty(request.Name))
            {
                updates.Add(update.Set(user1 => user1.Name, request.Name));
            }

            // Surname modification
            if (!string.IsNullOrEmpty(request.Surname))
            {
                updates.Add(update.Set(user1 => user1.Surname, request.Surname));
            }

            // Username modification
            if (!string.IsNullOrEmpty(request.Username))
            {
                updates.Add(update.Set(user1 => user1.Username, request.Username));
            }

            // Email modification, passing through email mod API
            if (MyRegex().IsMatch(request.Email))
            {
                //TODO chiamata all'API di aggiornamento della mail 
            }

            // ReoleID modification, assume roleID is correct
            if (!string.IsNullOrEmpty(request.Roleid))
            {
                updates.Add(update.Set(user1 => user1.RoleId, request.Roleid));
            }

            // Path to profile image modification
            if (!string.IsNullOrEmpty(request.Pathtopic))
            {
                updates.Add(update.Set(user1 => user1.PathToPic, request.Pathtopic));
            }

            // FA enabling or disabling
            if (request.HasIsfaenabled)
            {
                updates.Add(update.Set(user1 => user1.IsFaEnable, request.Isfaenabled));
            }

            // Block / Unblock account
            if (request.HasIsblocked)
            {
                updates.Add(update.Set(user1 => user1.IsBlocked, request.Isblocked));
            }
            
            // Biography modification, can only change own bio
            if (!string.IsNullOrEmpty(request.Biography) /*&& user.Id == request.Id*/)
            {
                updates.Add(update.Set(user1 => user1.Biography, request.Biography));
            }
            
            // Chats modification, expects new full set of chats every time
            if (request.Chats.Any())
            {
                updates.Add(update.Set<string[]>(user1 => user1.ChatsCollectionNames, request.Chats.ToArray()));
            }
            
            // Combination of all modifications, only if list is not empty
            try
            {
                if(updates.Any()) await collection.UpdateOneAsync(filter, update.Combine(updates));
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
        else if(user.Id == request.Id) // if user is trying to modify his own account
        {
            var update = Builders<User>.Update;
            var updates = new List<UpdateDefinition<User>>();
            var filter = Builders<User>.Filter.Eq(user1 => user1.Id, user.Id);

            // Email modification, passing through email mod API
            if (MyRegex().IsMatch(request.Email))
            {
                //TODO chiamata all'API di aggiornamento della mail 
            }

            // Path to profile pic modification
            if (!string.IsNullOrEmpty(request.Pathtopic))
            {
                updates.Add(update.Set(user1 => user1.PathToPic, request.Pathtopic));
            }

            // Biography modification
            if (!string.IsNullOrEmpty(request.Biography))
            {
                updates.Add(update.Set(user1 => user1.Biography, request.Biography));
            }
            
            // Chats modification, expects new full set of chats every time
            if (request.Chats.Any())
            {
                updates.Add(update.Set<string[]>(user1 => user1.ChatsCollectionNames, request.Chats.ToArray()));
            }
            
            // Combination of all modifications, only if list is not empty
            try
            {
                if(updates.Any()) await collection.UpdateOneAsync(filter, update.Combine(updates));
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
}