using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using RedBoxAuth.Email_utility;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Session_storage;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using RedBoxAuthentication;
using Shared;
using Shared.Models;
using Status = Grpc.Core.Status;

namespace RedBoxAuth.Services;

/// <inheritdoc />
public class AuthenticationService : AuthenticationGrpcService.AuthenticationGrpcServiceBase
{
	private readonly AuthSettings _authOptions;
	private readonly IAuthEmailUtility _emailUtility;
	private readonly ISecurityHashUtility _hashUtility;
	private readonly IPasswordUtility _passwordUtility;
	private readonly IMongoCollection<Role> _roleCollection;
	private readonly ISessionStorage _sessionStorage;
	private readonly ITotpUtility _totp;
	private readonly IMongoCollection<User> _userCollection;

	/// <inheritdoc />
	public AuthenticationService(IOptions<AccountDatabaseSettings> dbOptions, ITotpUtility totp,
		ISessionStorage sessionStorage,
		IPasswordUtility passwordUtility, IOptions<AuthSettings> authOptions, ISecurityHashUtility hashUtility,
		IAuthEmailUtility emailUtility)
	{
		_totp = totp;
		_hashUtility = hashUtility;
		_emailUtility = emailUtility;
		_passwordUtility = passwordUtility;
		_authOptions = authOptions.Value;
		_sessionStorage = sessionStorage;

		var mongoClient = new MongoClient(dbOptions.Value.ConnectionString);
		var db = mongoClient.GetDatabase(dbOptions.Value.DatabaseName);
		_userCollection = db.GetCollection<User>(dbOptions.Value.UsersCollection);
		_roleCollection = db.GetCollection<Role>(dbOptions.Value.RolesCollection);
	}

	/// <inheritdoc />
	public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
	{
		try
		{
			if (context.GetHttpContext().Request.Headers.ContainsKey(Constants.TokenHeaderName) &&
			    await _sessionStorage.TokenExistsAsync(context.GetHttpContext().Request
				    .Headers[Constants.TokenHeaderName]))
				return new LoginResponse
				{
					Status = LoginStatus.AlreadyLogged
				};

			User? user;

			switch (request.IdentifierCase)
			{
				default:
				case LoginRequest.IdentifierOneofCase.None:
					return new LoginResponse
					{
						Status = LoginStatus.MissingParameter
					};
				case LoginRequest.IdentifierOneofCase.Username:
					user = await _userCollection.Find(u => u.Username == request.Username.Normalize())
						.FirstOrDefaultAsync();
					break;
				case LoginRequest.IdentifierOneofCase.Email:
					user = await _userCollection.Find(u => u.Email == request.Email.Normalize()).FirstOrDefaultAsync();
					break;
			}

			if (user is null)
				return new LoginResponse
				{
					Status = LoginStatus.InvalidCredentials
				};

			if (user.IsBlocked)
				return new LoginResponse
				{
					Status = LoginStatus.IsBlocked
				};

			if (!_passwordUtility.VerifyPassword(request.Password, user.Salt, user.PasswordHash))
			{
				var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
				var updates = Builders<User>.Update.Inc(u => u.InvalidLoginAttempts, 1);
				await _userCollection.FindOneAndUpdateAsync(filter, updates);
				user.InvalidLoginAttempts++;

				if (user.InvalidLoginAttempts < _authOptions.MaxLoginAttempts)
					return new LoginResponse
					{
						Status = LoginStatus.InvalidCredentials,
						AttemptsLeft = _authOptions.MaxLoginAttempts - user.InvalidLoginAttempts
					};

				updates = Builders<User>.Update.Set(u => u.IsBlocked, true);
				await _userCollection.FindOneAndUpdateAsync(filter, updates);
				await _emailUtility.SendAccountLockNotificationAsync(user.Email, user.Username);

				return new LoginResponse
				{
					Status = LoginStatus.IsBlocked
				};
			}

			if (_sessionStorage.IsUserAlreadyLogged(user.Username, out var token, out var remainingTime))
				return new LoginResponse
				{
					Status = LoginStatus.LoginSuccess,
					Token = token,
					ExpiresAt = remainingTime
				};

			user.Role = await _roleCollection.Find(r => r.Id == user.RoleId).FirstOrDefaultAsync();

			user.SecurityHash = _hashUtility.Calculate(context.GetHttpContext().Request.Headers["User-Agent"],
				context.GetHttpContext().Connection.RemoteIpAddress);

			if (user.IsFaEnable)
			{
				var result = await _sessionStorage.StorePendingAsync(user);
				return new LoginResponse
				{
					Status = LoginStatus.Require2Fa,
					Token = result.token,
					ExpiresAt = result.expiresAt
				};
			}

			user.IsAuthenticationCompleted = true;
			var key = await _sessionStorage.StoreAsync(user);

			await _userCollection.FindOneAndUpdateAsync(Builders<User>.Filter.Eq(u => u.Id, user.Id),
				Builders<User>.Update.Set(u => u.LastAccess, DateTime.UtcNow).Set(u => u.InvalidLoginAttempts, 0));

			return new LoginResponse
			{
				Status = LoginStatus.LoginSuccess,
				Token = key.token,
				ExpiresAt = key.expiresAt
			};
		}
		catch (Exception e)
		{
			throw new RpcException(new Status(StatusCode.Internal, e.Message));
		}
	}

	/// <inheritdoc />
	[AuthenticationRequired]
	public override async Task<Empty> Logout(Empty request, ServerCallContext context)
	{
		var key = context.GetHttpContext().Request.Headers[Constants.TokenHeaderName];

		await _sessionStorage.DeleteAsync(key);

		return new Empty();
	}

	/// <inheritdoc />
	public override async Task<TwoFactorResponse> Verify2FA(TwoFactorRequest request, ServerCallContext context)
	{
		if (!_sessionStorage.TryToGet(request.Token, out var user))
			return new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.UserNotLogged
			};


		if (!user!.IsFaEnable)
			return new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.TfaNotEnabled
			};

		if (user.IsAuthenticationCompleted)
			return new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.AlreadyVerified
			};

		if (!_totp.VerifyCode(user.FaSeed!, request.TwoFaCode))
			return new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.InvalidCode
			};

		var expiresAt = await _sessionStorage.SetCompletedAsync(request.Token);

		return new TwoFactorResponse
		{
			Code = TwoFactorResponseCode.ValidCode,
			TokenExpiresAt = expiresAt
		};
	}

	/// <inheritdoc />
	[AuthenticationRequired]
	public override async Task<TokenRefreshResponse> RefreshToken(Empty request, ServerCallContext context)
	{
		var token = await _sessionStorage.RefreshTokenAsync(context.GetHttpContext().Request
			.Headers[Constants.TokenHeaderName]!);

		return new TokenRefreshResponse
		{
			Token = token.newToken,
			ExpiresAt = token.expiresAt
		};
	}

	/// <inheritdoc />
	public override async Task<Result> ForgottenPassword(PasswordResetRequest request, ServerCallContext context)
	{
		User? user;

		switch (request.IdentifierCase)
		{
			default:
			case PasswordResetRequest.IdentifierOneofCase.None:
				return new Result
				{
					Status = Shared.Status.MissingParameters
				};

			case PasswordResetRequest.IdentifierOneofCase.EmailAddress:
				user = await _userCollection.Find(u => u.Email == request.EmailAddress.Normalize())
					.FirstOrDefaultAsync();
				break;

			case PasswordResetRequest.IdentifierOneofCase.Username:
				user = await _userCollection.Find(u => u.Username == request.Username.Normalize())
					.FirstOrDefaultAsync();
				break;
		}


		if (user is null)
			return new Result
			{
				Status = Shared.Status.Error,
				Error = "User not exists"
			};

		if (user.IsBlocked)
			return new Result
			{
				Status = Shared.Status.Error,
				Error = "User is blocked"
			};

		await _emailUtility.SendPasswordResetRequestAsync(user.Email, user.Username, user.Id);

		return new Result
		{
			Status = Shared.Status.Ok
		};
	}
}