using System.Security.Cryptography;
using MemoryPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RedBoxAuth.Models;
using RedBoxAuth.Settings;
using StackExchange.Redis;

namespace RedBoxAuth.Cache;

public class AuthCache : MemoryCache, IAuthCache
{
	private static uint _scanMinutes;
	private readonly AuthenticationOptions _authOpts;
	private readonly AuthenticationOptions _options;
	private readonly IDatabase _redis;

	public AuthCache(IConnectionMultiplexer redis, IOptions<AuthenticationOptions> authOpt,
		IOptions<AuthenticationOptions> options) : base(
		new MemoryCacheOptions
			{ ExpirationScanFrequency = TimeSpan.FromMinutes(_scanMinutes) })
	{
		_options = options.Value;
		_redis = redis.GetDatabase();
		_scanMinutes = authOpt.Value.LocalCacheExpirationScanMinutes;
		_authOpts = authOpt.Value;
	}

	public string Store(User user)
	{
		var key = GenerateToken();
		if (!_redis.HashSet(_options.UsersHashKey, user.Username, key, When.NotExists))
		{
			key = _redis.HashGet(_options.UsersHashKey, user.Username);
			if (KeyExists(key))
				return key!;

			StoreLocal(user, key!, _authOpts.SessionExpireMinutes);
			return key!;
		}

		user.IsAuthenticated = true;
		StoreRedis(user, key, _authOpts.SessionExpireMinutes);
		StoreLocal(user, key, _authOpts.SessionExpireMinutes);
		return key;
	}

	public string StorePending(User user)
	{
		var key = GenerateToken();
		if (!_redis.HashSet(_options.UsersHashKey, user.Username, key, When.NotExists))
		{
			key = _redis.HashGet(_options.UsersHashKey, user.Username);
			return key!;
		}

		StoreRedis(user, key, _authOpts.PendingAuthMinutes);
		return key;
	}

	public bool KeyExists(string? key)
	{
		return _redis.KeyExists(key);
	}

	public void SetCompleted(string key)
	{
		var user = RedisGet(key, out _);
		_redis.KeyDeleteAsync(key, CommandFlags.FireAndForget);

		user!.IsAuthenticated = true;

		StoreRedis(user, key, _authOpts.SessionExpireMinutes);
		StoreLocal(user, key, _authOpts.SessionExpireMinutes);
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

	public async void RefreshExpireTime(string key)
	{
		await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(_authOpts.SessionExpireMinutes),
			CommandFlags.FireAndForget);

		var user = this.Get<User>(key);
		if (user is not null)
			Remove(key);
		else
			user = RedisGet(key, out _);

		StoreLocal(user!, key, _authOpts.SessionExpireMinutes);
	}

	private string GenerateToken()
	{
		var rndBuff = new byte[_authOpts.TokenSizeBytes];
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
		await _redis.StringSetAsync(key, serialized, TimeSpan.FromMinutes(minutes), When.NotExists,
			CommandFlags.FireAndForget);
	}

	private void StoreLocal(User user, string key, uint minutes)
	{
		this.Set(key, user, TimeSpan.FromMinutes(minutes));
	}

	private void StoreLocal(User user, string key, TimeSpan minutes)
	{
		this.Set(key, user, minutes);
	}

	private User? RedisGet(string key, out TimeSpan remainingTime)
	{
		var stream = (byte[]?)_redis.StringGet(key);
		remainingTime = _redis.KeyTimeToLive(key)!.Value;
		return MemoryPackSerializer.Deserialize<User>(stream);
	}
}