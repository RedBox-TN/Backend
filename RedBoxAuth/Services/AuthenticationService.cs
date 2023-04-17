using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using RedBoxAuth.Cache;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using RedBoxAuthentication;
using Shared.Models;

namespace RedBoxAuth.Services;

/// <inheritdoc />
[Anonymous]
public class AuthenticationService : AuthenticationGrpcService.AuthenticationGrpcServiceBase
{
	private const string HeaderName = Constants.TokenHeaderName;

	private readonly IAuthCache _authCache;
	private readonly AuthenticationOptions _authOptions;
	private readonly ISecurityHashUtility _hashUtility;
	private readonly IPasswordUtility _passwordUtility;
	private readonly IMongoCollection<Role> _roleCollection;
	private readonly ITotpUtility _totp;
	private readonly IMongoCollection<User> _userCollection;


	/// <inheritdoc />
	public AuthenticationService(IOptions<AccountDatabaseSettings> dbOptions, ITotpUtility totp, IAuthCache authCache,
		IPasswordUtility passwordUtility, IOptions<AuthenticationOptions> authOptions, ISecurityHashUtility hashUtility)
	{
		_totp = totp;
		_hashUtility = hashUtility;
		_passwordUtility = passwordUtility;
		_authOptions = authOptions.Value;
		_authCache = authCache;

		var mongoClient = new MongoClient(dbOptions.Value.ConnectionString);
		var db = mongoClient.GetDatabase(dbOptions.Value.DatabaseName);
		_userCollection = db.GetCollection<User>(dbOptions.Value.UsersCollection);
		_roleCollection = db.GetCollection<Role>(dbOptions.Value.RolesCollection);
	}

	/// <inheritdoc />
	public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
	{
		if (context.GetHttpContext().Request.Headers.ContainsKey(HeaderName) &&
		    _authCache.KeyExists(context.GetHttpContext().Request.Headers[HeaderName]))
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
			var filter = Builders<User>.Filter.Eq("Id", user.Id);
			var updates = Builders<User>.Update.Inc("InvalidLoginAttempts", 1);
			await _userCollection.FindOneAndUpdateAsync(filter, updates);
			user.InvalidLoginAttempts++;

			if (user.InvalidLoginAttempts < _authOptions.MaxLoginAttempts)
				return new LoginResponse
				{
					Status = LoginStatus.InvalidCredentials,
					AttemptsLeft = _authOptions.MaxLoginAttempts - user.InvalidLoginAttempts
				};

			updates = Builders<User>.Update.Set("IsBlocked", true);
			await _userCollection.FindOneAndUpdateAsync(filter, updates);
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

		await _userCollection.FindOneAndUpdateAsync(Builders<User>.Filter.Eq("Id", user.Id),
			Builders<User>.Update.Set("LastAccess", DateTime.Now).Set("InvalidLoginAttempts", 0));

		return new LoginResponse
		{
			Status = LoginStatus.LoginSuccess,
			Token = key,
			ExpiresAt = expireAt
		};
	}

	/// <inheritdoc />
	public override Task<LogoutResponse> Logout(Nil request, ServerCallContext context)
	{
		if (!context.GetHttpContext().Request.Headers.TryGetValue(HeaderName, out var key) ||
		    string.IsNullOrEmpty(key))
			return Task.FromResult(new LogoutResponse
			{
				Status = LogoutStatus.NotLogged
			});

		_authCache.DeleteAsync(key);

		return Task.FromResult(new LogoutResponse
		{
			Status = LogoutStatus.LogoutSuccess
		});
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


		if (!_totp.VerifyCode(user.FaSeed, request.TwoFaCode))
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
	public override Task<TokenRefreshResponse> RefreshToken(Nil request, ServerCallContext context)
	{
		if (!context.GetHttpContext().Request.Headers.ContainsKey(HeaderName) ||
		    !_authCache.KeyExists(context.GetHttpContext().Request.Headers[HeaderName]))
			return Task.FromResult(new TokenRefreshResponse
			{
				Status = RefreshTokenStatusCode.InvalidToken
			});


		var token = _authCache.RefreshToken(context.GetHttpContext().Request.Headers[HeaderName]!, out var expiresAt);
		return Task.FromResult(new TokenRefreshResponse
		{
			Status = RefreshTokenStatusCode.TokenRefreshed,
			Token = token,
			ExpiresAt = expiresAt
		});
	}
}