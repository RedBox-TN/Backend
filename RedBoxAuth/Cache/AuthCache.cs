using System.Security.Cryptography;
using MemoryPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;
using Shared.Models;
using StackExchange.Redis;
using ZstdSharp;

namespace RedBoxAuth.Cache;

/// <summary>
///     Implementation of IAuthCache, combining redis and local cache
/// </summary>
public class AuthCache : MemoryCache, IAuthCache

{
    private static uint _scanMinutes;
    private readonly AuthSettings _authSettings;
    private readonly IDatabase _sessionDb;
    private readonly IDatabase _tokenDb;

    /// <inheritdoc />
    public AuthCache(IConnectionMultiplexer redis, IOptions<AuthSettings> options,
        IOptions<RedisSettings> redisSettings) : base(
        new MemoryCacheOptions
            { ExpirationScanFrequency = TimeSpan.FromMinutes(_scanMinutes) })
    {
        _authSettings = options.Value;
        _sessionDb = redis.GetDatabase(redisSettings.Value.SessionDatabaseIndex);
        _tokenDb = redis.GetDatabase(redisSettings.Value.UsernameTokenDatabaseIndex);
        _scanMinutes = options.Value.LocalCacheExpirationScanMinutes;
    }

    /// <inheritdoc />
    public string Store(User user, out long expireAt)
    {
        var key = GenerateToken();
        if (IsUserAlreadyLogged(user.Username))
        {
            key = _tokenDb.StringGet(user.Username);
            expireAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
            return key!;
        }

        user.IsAuthenticated = true;
        StoreRedis(user, key, _authSettings.SessionExpireMinutes);
        expireAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
        StoreLocal(user, key, _authSettings.SessionExpireMinutes);
        return key;
    }

    /// <inheritdoc />
    public string StorePending(User user, out long expireAt)
    {
        var key = GenerateToken();
        if (IsUserAlreadyLogged(user.Username))
        {
            key = _tokenDb.StringGet(user.Username);
            expireAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
            return key!;
        }

        StoreRedis(user, key, _authSettings.PendingAuthMinutes);
        expireAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
        return key;
    }

    /// <inheritdoc />
    public bool TokenExists(string? key)
    {
        return _sessionDb.KeyExists(key);
    }

    public bool IsUserAlreadyLogged(string? username)
    {
        return _tokenDb.KeyExists(username);
    }

    /// <inheritdoc />
    public void SetCompleted(string key, out long expiresAt)
    {
        var user = RedisGet(key, out _);
        _sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        user!.IsAuthenticated = true;

        StoreRedis(user, key, _authSettings.SessionExpireMinutes);
        expiresAt = ((DateTimeOffset)_sessionDb.KeyExpireTime(key)!.Value).ToUnixTimeMilliseconds();
        StoreLocal(user, key, _authSettings.SessionExpireMinutes);
    }

    /// <inheritdoc />
    public async void DeleteAsync(string? key)
    {
        if (!TryToGet(key, out var user)) return;

        var username = user!.Username;
        await _sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        await _tokenDb.KeyDeleteAsync(username, CommandFlags.FireAndForget);
        if (key != null) Remove(key);
    }

    /// <inheritdoc />
    public bool TryToGet(string? key, out User? user)
    {
        if (!TokenExists(key))
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

        RenameLocal(oldToken, token);

        return token;
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

    private async void StoreRedis(User user, string key, uint minutes)
    {
        var serialized = MemoryPackSerializer.Serialize(user);

        using var compressor = new Compressor(Compressor.MaxCompressionLevel);

        await _tokenDb.StringSetAsync(user.Username, key, TimeSpan.FromMinutes(_authSettings.SessionExpireMinutes - 1),
            When.NotExists, CommandFlags.FireAndForget);

        await _sessionDb.StringSetAsync(key, compressor.Wrap(serialized).ToArray(),
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
        StoreLocal(user, newToken, _authSettings.SessionExpireMinutes);
    }

    private User? RedisGet(string key, out TimeSpan remainingTime)
    {
        if (!TokenExists(key))
        {
            remainingTime = TimeSpan.Zero;
            return null;
        }

        var stream = (byte[]?)_sessionDb.StringGet(key);
        remainingTime = _sessionDb.KeyTimeToLive(key)!.Value;

        using var decompressor = new Decompressor();
        return MemoryPackSerializer.Deserialize<User>(decompressor.Unwrap(stream));
    }
}