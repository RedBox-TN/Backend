using MemoryPack;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;
using Shared.Models;
using StackExchange.Redis;
using ZstdSharp;

namespace RedBoxAuth.Cache;

/// <inheritdoc />
public class BasicAuthCache : IBasicAuthCache
{
	// contains serialized users associated to the token
	private readonly IDatabase _sessionDb;

	// contains current token associated to the username
	private readonly IDatabase _tokenDb;

	/// <summary>
	///     Constructor for dependency injector container
	/// </summary>
	/// <param name="redis">Redis connection</param>
	/// <param name="redisSettings">Redis settings</param>
	public BasicAuthCache(IConnectionMultiplexer redis, IOptions<RedisSettings> redisSettings)
	{
		_sessionDb = redis.GetDatabase(redisSettings.Value.SessionDatabaseIndex);
		_tokenDb = redis.GetDatabase(redisSettings.Value.UsernameTokenDatabaseIndex);
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
	public async Task DeleteAsync(string? key)
	{
		if (!TryToGet(key, out var user)) return;

		var username = user!.Username;
		await _sessionDb.KeyDeleteAsync(key, CommandFlags.FireAndForget);
		await _tokenDb.KeyDeleteAsync(username, CommandFlags.FireAndForget);
	}
}