using System.Runtime.InteropServices.JavaScript;
using System.Text.RegularExpressions;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Email_utility;
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

    //[PermissionsRequired(DefaultPermissions.ManageUsersAccounts)]
    public override async Task<Result> CreateUser(GrpcUser request, ServerCallContext context)
    {
        var password = _passwordUtility.GeneratePassword();
        var salt = _passwordUtility.CreateSalt();
        var passwordHash = _passwordUtility.HashPassword(password, salt);
        
        var collection = _database.GetCollection<User>(_settings.UsersCollection);

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
                PasswordHistory = new List<byte[]> { passwordHash }
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
}