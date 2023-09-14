using System.Security.Cryptography;
using MemoryPack;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;
using Shared.Models;
using StackExchange.Redis;
using ZstdSharp;

namespace RedBoxAuth.Cache;

/// <summary>
///     Implementation of IAuthCache
/// </summary>
public class AuthCache : IAuthCache
{
	private readonly AuthSettings _authSettings;

	// contains serialized users associated to the token
	private readonly IDatabase _sessionDb;

	// contains current token associated to the username
	private readonly IDatabase _tokenDb;

	public AuthCache(IConnectionMultiplexer redis, IOptions<AuthSettings> options,
		IOptions<RedisSettings> redisSettings)
	{
		_authSettings = options.Value;
		_sessionDb = redis.GetDatabase(redisSettings.Value.SessionDatabaseIndex);
		_tokenDb = redis.GetDatabase(redisSettings.Value.UsernameTokenDatabaseIndex);
	}

	/// <inheritdoc />
	public string Store(User user, out long expireAt)
	{
		var key = GenerateToken();
		user.IsAuthenticated = true;
		Store(user, key, _authSettings.SessionExpireMinutes);
		expireAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
		return key;
	}

	/// <inheritdoc />
	public string StorePending(User user, out long expireAt)
	{
		var key = GenerateToken();
		Store(user, key, _authSettings.PendingAuthMinutes);
		expireAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
		return key;
	}

	/// <inheritdoc />
	public bool TokenExists(string? key)
	{
		return _sessionDb.KeyExists(key);
	}

	/// <inheritdoc />
	public void SetCompleted(string key, out long expiresAt)
	{
		TryToGet(key, out var user);
		_sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);

		user!.IsAuthenticated = true;

		Store(user, key, _authSettings.SessionExpireMinutes);
		expiresAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
	}

	/// <inheritdoc />
	public async void DeleteAsync(string? key)
	{
		if (!TryToGet(key, out var user)) return;

		var username = user!.Username;
		await _sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);
		await _tokenDb.KeyDeleteAsync(username, CommandFlags.FireAndForget);
	}

	/// <inheritdoc />
	public bool TryToGet(string? token, out User? user)
	{
		if (!TokenExists(token))
		{
			user = null;
			return false;
		}

		var stream = (byte[]?)_sessionDb.StringGet(token);

		using var decompressor = new Decompressor();
		user = MemoryPackSerializer.Deserialize<User>(decompressor.Unwrap(stream));
		return true;
	}

	/// <inheritdoc />
	public string RefreshToken(string oldToken, out long expiresAt)
	{
		var token = GenerateToken();

		TryToGet(oldToken, out var user);

		_sessionDb.KeyRenameAsync(oldToken, token);
		_tokenDb.StringSetAsync(user!.Username, token, TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes - 1),
			When.Always, CommandFlags.FireAndForget);
		_sessionDb.KeyExpireAsync(token, TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes));
		expiresAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(token)!.Value).ToUnixTimeMilliseconds();

		return token;
	}

	/// <inheritdoc />
	public bool IsUserAlreadyLogged(string? username, out string? token, out long remainingTime)
	{
		token = null;
		remainingTime = 0;
		if (!_tokenDb.KeyExists(username)) return false;

		token = _tokenDb.StringGet(username);
		remainingTime = ((DateTimeOffset)_sessionDb.KeyExpireTime(token)!.Value).ToUnixTimeMilliseconds();
		return true;
	}

	private string GenerateToken()
	{
		var rndBuff = new byte[_authSettings.TokenSizeBytes];
		RandomNumberGenerator.Fill(rndBuff);
		var token = Convert.ToBase64String(rndBuff);

		while (_sessionDb.KeyExists(token))
		{
			RandomNumberGenerator.Fill(rndBuff);
			token = Convert.ToBase64String(rndBuff);
		}

		return token;
	}

	private async void Store(User user, string key, uint minutes)
	{
		var serialized = MemoryPackSerializer.Serialize(user);

		using var compressor = new Compressor(Compressor.MaxCompressionLevel);

		await _tokenDb.StringSetAsync(user.Username, key, TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes - 1),
			When.NotExists, CommandFlags.FireAndForget);

		await _sessionDb.StringSetAsync(key, compressor.Wrap(serialized).ToArray(),
			TimeSpan.FromMinutes(minutes), When.NotExists, CommandFlags.FireAndForget);
	}
}