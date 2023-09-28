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
	public async Task<(string token, long expiresAt)> StoreAsync(User user)
	{
		var key = await GenerateTokenAsync();
		user.IsAuthenticated = true;
		await SerializeCompressStoreAsync(user, key, _authSettings.SessionExpireMinutes);
		return (key, ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds());
	}

	/// <inheritdoc />
	public async Task<(string token, long expiresAt)> StorePendingAsync(User user)
	{
		var key = await GenerateTokenAsync();
		await SerializeCompressStoreAsync(user, key, _authSettings.PendingAuthMinutes);
		return (key, ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds());
	}

	/// <inheritdoc />
	public Task<bool> TokenExistsAsync(string? key)
	{
		return _sessionDb.KeyExistsAsync(key);
	}

	/// <inheritdoc />
	public async Task<long> SetCompletedAsync(string key)
	{
		TryToGet(key, out var user);
		await _sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);

		user!.IsAuthenticated = true;

		await SerializeCompressStoreAsync(user, key, _authSettings.SessionExpireMinutes);
		return ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
	}

	/// <inheritdoc />
	public async Task DeleteAsync(string? key)
	{
		if (!TryToGet(key, out var user)) return;

		var username = user!.Username;
		await _sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);
		await _tokenDb.KeyDeleteAsync(username, CommandFlags.FireAndForget);
	}

	/// <inheritdoc />
	public bool TryToGet(string? token, out User? user)
	{
		if (!_sessionDb.KeyExists(token))
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
	public async Task<(string newToken, long expiresAt)> RefreshTokenAsync(string oldToken)
	{
		var token = await GenerateTokenAsync();

		var stream = (byte[]?)_sessionDb.StringGet(token);

		using var decompressor = new Decompressor();
		var user = MemoryPackSerializer.Deserialize<User>(decompressor.Unwrap(stream));

		await _sessionDb.KeyRenameAsync(oldToken, token);
		await _tokenDb.StringSetAsync(user!.Username, token,
			TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes - 1),
			When.Always, CommandFlags.FireAndForget);
		await _sessionDb.KeyExpireAsync(token, TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes));

		return (token, ((DateTimeOffset)_sessionDb.KeyExpireTime(token)!.Value).ToUnixTimeMilliseconds());
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

	private async Task<string> GenerateTokenAsync()
	{
		var rndBuff = new byte[_authSettings.TokenSizeBytes];
		RandomNumberGenerator.Fill(rndBuff);
		var token = Convert.ToBase64String(rndBuff);

		while (await _sessionDb.KeyExistsAsync(token))
		{
			RandomNumberGenerator.Fill(rndBuff);
			token = Convert.ToBase64String(rndBuff);
		}

		return token;
	}

	private async Task SerializeCompressStoreAsync(User user, string key, uint minutes)
	{
		var serialized = MemoryPackSerializer.Serialize(user);

		using var compressor = new Compressor(Compressor.MaxCompressionLevel);

		await _tokenDb.StringSetAsync(user.Username, key, TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes - 1),
			When.NotExists, CommandFlags.FireAndForget);

		await _sessionDb.StringSetAsync(key, compressor.Wrap(serialized).ToArray(),
			TimeSpan.FromMinutes(minutes), When.NotExists, CommandFlags.FireAndForget);
	}
}