using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using RedBoxAuth.Cache;
using RedBoxAuth.Email_utility;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using RedBoxAuthentication;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBoxAuth.Services;

/// <inheritdoc />
public class AuthenticationService : AuthenticationGrpcService.AuthenticationGrpcServiceBase
{
	private readonly IAuthCache _authCache;
	private readonly AuthSettings _authOptions;
	private readonly AuthEmailSettings _emailSettings;
	private readonly IAuthEmailUtility _emailUtility;
	private readonly ISecurityHashUtility _hashUtility;
	private readonly IPasswordUtility _passwordUtility;
	private readonly IMongoCollection<Role> _roleCollection;
	private readonly ITotpUtility _totp;
	private readonly IMongoCollection<User> _userCollection;

	/// <inheritdoc />
	public AuthenticationService(IOptions<AccountDatabaseSettings> dbOptions, ITotpUtility totp, IAuthCache authCache,
		IPasswordUtility passwordUtility, IOptions<AuthSettings> authOptions, ISecurityHashUtility hashUtility,
		IAuthEmailUtility emailUtility, IOptions<AuthEmailSettings> emailOptions)
	{
		_totp = totp;
		_hashUtility = hashUtility;
		_emailUtility = emailUtility;
		_passwordUtility = passwordUtility;
		_authOptions = authOptions.Value;
		_authCache = authCache;
		_emailSettings = emailOptions.Value;

		var mongoClient = new MongoClient(dbOptions.Value.ConnectionString);
		var db = mongoClient.GetDatabase(dbOptions.Value.DatabaseName);
		_userCollection = db.GetCollection<User>(dbOptions.Value.UsersCollection);
		_roleCollection = db.GetCollection<Role>(dbOptions.Value.RolesCollection);
	}

	/// <inheritdoc />
	public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
	{
		if (context.GetHttpContext().Request.Headers.ContainsKey(Constants.TokenHeaderName) &&
		    _authCache.TokenExists(context.GetHttpContext().Request.Headers[Constants.TokenHeaderName]))
			return new LoginResponse
			{
				Status = LoginStatus.AlreadyLogged
			};

		User? user;
		if (request.HasUsername)
			user = await _userCollection.Find(u => u.Username == request.Username.Normalize()).FirstOrDefaultAsync();
		else
			user = await _userCollection.Find(u => u.Email == request.Email.Normalize()).FirstOrDefaultAsync();

		if (user is null)
			return new LoginResponse
			{
				Status = LoginStatus.UserNotExist
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
			_emailUtility.SendAccountLockNotificationAsync(user.Email, user.Username);

			return new LoginResponse
			{
				Status = LoginStatus.IsBlocked
			};
		}

		user.Role = await _roleCollection.Find(r => r.Id == user.RoleId).FirstOrDefaultAsync();

		user.SecurityHash = _hashUtility.Calculate(context.GetHttpContext().Request.Headers["User-Agent"],
			context.GetHttpContext().Connection.RemoteIpAddress);

		long expireAt;
		if (user.IsFaEnable)
			return new LoginResponse
			{
				Status = LoginStatus.Require2Fa,
				Token = _authCache.StorePending(user, out expireAt),
				ExpiresAt = expireAt
			};

		user.IsAuthenticated = true;
		var key = _authCache.Store(user, out expireAt);

		await _userCollection.FindOneAndUpdateAsync(Builders<User>.Filter.Eq(u => u.Id, user.Id),
			Builders<User>.Update.Set(u => u.LastAccess, DateTime.Now).Set(u => u.InvalidLoginAttempts, 0));

		return new LoginResponse
		{
			Status = LoginStatus.LoginSuccess,
			Token = key,
			ExpiresAt = expireAt
		};
	}

	/// <inheritdoc />
	[AuthenticationRequired]
	public override Task<Empty> Logout(Empty request, ServerCallContext context)
	{
		var key = context.GetHttpContext().Request.Headers[Constants.TokenHeaderName];

		_authCache.DeleteAsync(key);

		return Task.FromResult(new Empty());
	}

	/// <inheritdoc />
	public override Task<TwoFactorResponse> Verify2FA(TwoFactorRequest request, ServerCallContext context)
	{
		if (!_authCache.TryToGet(request.Token, out var user))
			return Task.FromResult(new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.UserNotLogged
			});


		if (!user!.IsFaEnable)
			return Task.FromResult(new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.TfaNotEnabled
			});

		if (user.IsAuthenticated)
			return Task.FromResult(new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.AlreadyVerified
			});

		if (!_totp.VerifyCode(user.FaSeed!, request.TwoFaCode))
			return Task.FromResult(new TwoFactorResponse
			{
				Code = TwoFactorResponseCode.InvalidCode
			});

		_authCache.SetCompleted(request.Token, out var expiresAt);

		return Task.FromResult(new TwoFactorResponse
		{
			Code = TwoFactorResponseCode.ValidCode,
			TokenExpiresAt = expiresAt
		});
	}

	/// <inheritdoc />
	[AuthenticationRequired]
	public override Task<TokenRefreshResponse> RefreshToken(Empty request, ServerCallContext context)
	{
		var token = _authCache.RefreshToken(context.GetHttpContext().Request.Headers[Constants.TokenHeaderName]!,
			out var expiresAt);

		return Task.FromResult(new TokenRefreshResponse
		{
			Token = token,
			ExpiresAt = expiresAt
		});
	}

	/// <inheritdoc />
	public override async Task<Result> ForgottenPassword(PasswordResetRequest request, ServerCallContext context)
	{
		var user = await _userCollection.Find(u => u.Username == request.EmailAddress).FirstOrDefaultAsync();
		if (user is null)
			return new Result
			{
				Status = Status.Error,
				Error = "User not exists"
			};

		if (user.IsBlocked)
			return new Result
			{
				Status = Status.Error,
				Error = "User is locked"
			};

		_emailUtility.SendPasswordResetRequestAsync(user.Email, user.Id);

		return new Result
		{
			Status = Status.Ok
		};
	}
}