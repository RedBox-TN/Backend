using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBoxAuth.Authorization;
using RedBoxAuth.Cache;
using RedBoxAuth.Models;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using RedBoxAuthentication;

namespace RedBoxAuth.Services;

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

	public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
	{
		// L'utente e' gia' loggato?
		if (context.GetHttpContext().Request.Headers.ContainsKey(HeaderName) &&
		    _authCache.KeyExists(context.GetHttpContext().Request.Headers[HeaderName]))
			return new LoginResponse
			{
				Status = LoginStatus.AlreadyLogged
			};

		// L'utente esiste?
		User? user;
		if (request.HasUsername)
			user = await _userCollection.Find(u => u.Username == request.Username.Normalize()).FirstOrDefaultAsync();
		else
			user = await _userCollection.Find(u => u.Email == request.Email.Normalize()).FirstOrDefaultAsync();

		// L'utente esiste?
		if (user is null)
			return new LoginResponse
			{
				Status = LoginStatus.UserNotExist
			};

		// L'utente e' bloccato?
		if (user.IsBlocked)
			return new LoginResponse
			{
				Status = LoginStatus.IsBlocked
			};

		// La password e' corretta?
		if (!_passwordUtility.VerifyPassword(request.Password, user.PasswordHash, user.Salt))
		{
			// Incremento il contatore dei tentativi falliti
			var filter = Builders<User>.Filter.Eq("Id", user.Id);
			var updates = Builders<User>.Update.Inc("InvalidLoginAttempts", 1);
			await _userCollection.FindOneAndUpdateAsync(filter, updates);
			user.InvalidLoginAttempts++;

			// L'utente ha superato il limite dei tentativi?
			if (user.InvalidLoginAttempts < _authOptions.MaxLoginAttempts)
				return new LoginResponse
				{
					Status = LoginStatus.InvalidCredentials,
					AttemptsLeft = _authOptions.MaxLoginAttempts - user.InvalidLoginAttempts
				};

			// Se si blocco l'utente
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

		uint expireAt;
		// La 2fa e' attiva?
		if (user.IsFaEnable)
			return new LoginResponse
			{
				Status = LoginStatus.Require2Fa,
				Token = _authCache.StorePending(user, out expireAt),
				ExpiresAt = expireAt
			};

		user.IsAuthenticated = true;
		var key = _authCache.Store(user, out expireAt);

		// Imposto l'ultimo accesso e azzero i tentativi falliti
		await _userCollection.FindOneAndUpdateAsync(Builders<User>.Filter.Eq("Id", user.Id),
			Builders<User>.Update.Set("LastAccess", DateTime.Now).Set("InvalidLoginAttempts", 0));

		return new LoginResponse
		{
			Status = LoginStatus.LoginSuccess,
			Token = key,
			ExpiresAt = expireAt
		};
	}

	public override Task<LogoutResponse> Logout(Nil request, ServerCallContext context)
	{
		if (!context.GetHttpContext().Request.Headers.TryGetValue(HeaderName, out var key) ||
		    string.IsNullOrEmpty(key))
			return Task.FromResult(new LogoutResponse
			{
				Status = LogoutStatus.NotLogged
			});

		_authCache.Delete(key);

		return Task.FromResult(new LogoutResponse
		{
			Status = LogoutStatus.LogoutSuccess
		});
	}

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