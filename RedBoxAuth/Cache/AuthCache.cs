using System.Security.Cryptography;
using MemoryPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RedBoxAuth.Models;
using RedBoxAuth.Settings;
using StackExchange.Redis;
using ZstdSharp;

namespace RedBoxAuth.Cache;

public class AuthCache : MemoryCache, IAuthCache
{
	private static uint _scanMinutes;
	private readonly AuthenticationOptions _options;
	private readonly IDatabase _redis;

	public AuthCache(IConnectionMultiplexer redis, IOptions<AuthenticationOptions> options) : base(
		new MemoryCacheOptions
			{ ExpirationScanFrequency = TimeSpan.FromMinutes(_scanMinutes) })
	{
		_options = options.Value;
		_redis = redis.GetDatabase();
		_scanMinutes = options.Value.LocalCacheExpirationScanMinutes;
	}

	public string Store(User user, out uint expireAt)
	{
		var key = GenerateToken();
		if (!_redis.HashSet(_options.UsersHashKey, user.Username, key, When.NotExists))
		{
			key = _redis.HashGet(_options.UsersHashKey, user.Username);
			expireAt = (uint)_redis.KeyExpireTime(key)!.Value.Millisecond;
			if (!TryGetValue(key!, out _)) return key!;

			StoreLocal(user, key!, _options.SessionExpireMinutes);
			return key!;
		}

		user.IsAuthenticated = true;
		StoreRedis(user, key, _options.SessionExpireMinutes);
		expireAt = (uint)_redis.KeyExpireTime(key)!.Value.Millisecond;
		StoreLocal(user, key, _options.SessionExpireMinutes);
		return key;
	}

	public string StorePending(User user, out uint expireAt)
	{
		var key = GenerateToken();
		if (!_redis.HashSet(_options.UsersHashKey, user.Username, key, When.NotExists))
		{
			key = _redis.HashGet(_options.UsersHashKey, user.Username);
			expireAt = (uint)_redis.KeyExpireTime(key)!.Value.Millisecond;
			return key!;
		}

		StoreRedis(user, key, _options.PendingAuthMinutes);
		expireAt = (uint)_redis.KeyExpireTime(key)!.Value.Millisecond;
		return key;
	}

	public bool KeyExists(string? key)
	{
		return _redis.KeyExists(key);
	}

	public void SetCompleted(string key, out uint expiresAt)
	{
		var user = RedisGet(key, out _);
		_redis.KeyDeleteAsync(key, CommandFlags.FireAndForget);

		user!.IsAuthenticated = true;

		StoreRedis(user, key, _options.SessionExpireMinutes);
		expiresAt = (uint)_redis.KeyExpireTime(key)!.Value.Millisecond;
		StoreLocal(user, key, _options.SessionExpireMinutes);
	}

	public async void Delete(string? key)
	{
		if (key is null) return;

		var username = this.Get<User>(key)?.Username;
		await _redis.KeyDeleteAsync(key, CommandFlags.FireAndForget);
		await _redis.HashDeleteAsync(_options.UsersHashKey, username, CommandFlags.FireAndForget);
		Remove(key);
	}

	public bool TryToGet(string? key, out User? user)
	{
		if (!KeyExists(key))
		{
			user = null;
			return false;
		}

		if (!this.TryGetValue(key!, out user))
		{
			user = RedisGet(key!, out var ttl);
			StoreLocal(user!, key!, ttl);
			return true;
		}

		user = this.Get<User>(key!);
		return true;
	}

	public string RefreshToken(string oldToken, out uint expiresAt)
	{
		var token = GenerateToken();
		_redis.KeyRenameAsync(oldToken, token);

		TryToGet(oldToken, out var user);

		_redis.HashSetAsync(_options.UsersHashKey, user!.Username, token);

		_redis.KeyExpireAsync(token, TimeSpan.FromMinutes(_options.SessionExpireMinutes));

		expiresAt = (uint)_redis.KeyExpireTime(token)!.Value.Millisecond;

		RenameLocal(oldToken, token);

		return token;
	}

	private string GenerateToken()
	{
		var rndBuff = new byte[_options.TokenSizeBytes];
		RandomNumberGenerator.Fill(rndBuff);

		var token = Convert.ToBase64String(rndBuff);
		while (_redis.KeyExists(token))
		{
			RandomNumberGenerator.Fill(rndBuff);
			token = Convert.ToBase64String(rndBuff);
		}

		return token;
	}

	private async void StoreRedis(User user, string key, uint minutes)
	{
		var serialized = MemoryPackSerializer.Serialize(user);

		using var compressor = new Compressor(Compressor.MaxCompressionLevel);

		await _redis.StringSetAsync(key, compressor.Wrap(serialized).ToArray(),
			TimeSpan.FromMinutes(minutes), When.NotExists, CommandFlags.FireAndForget);
	}

	private void StoreLocal(User user, string key, uint minutes)
	{
		this.Set(key, user, TimeSpan.FromMinutes(minutes));
	}

	private void StoreLocal(User user, string key, TimeSpan minutes)
	{
		this.Set(key, user, minutes);
	}

	private void RenameLocal(string old, string newToken)
	{
		var user = this.Get<User>(old);
		if (user is null) return;

		Remove(old);
		StoreLocal(user, newToken, _options.SessionExpireMinutes);
	}

	private User? RedisGet(string key, out TimeSpan remainingTime)
	{
		var stream = (byte[]?)_redis.StringGet(key);
		remainingTime = _redis.KeyTimeToLive(key)!.Value;

		using var decompressor = new Decompressor();
		return MemoryPackSerializer.Deserialize<User>(decompressor.Unwrap(stream));
	}
}