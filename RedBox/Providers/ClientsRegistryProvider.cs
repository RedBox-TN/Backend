using System.Collections.Concurrent;
using Grpc.Core;
using RedBoxServices;

namespace RedBox.Providers;

public class ClientsRegistryProvider : IClientsRegistryProvider
{
	private readonly ConcurrentDictionary<string, IServerStreamWriter<ServerUpdate>> _clients = new();


	public void Add(string userId, IServerStreamWriter<ServerUpdate> connection)
	{
		_clients[userId] = connection;
	}

	public bool TryToGet(string userId, out IServerStreamWriter<ServerUpdate>? client)
	{
		return _clients.TryGetValue(userId, out client);
	}

	public void Remove(string userId)
	{
		_clients.Remove(userId, out _);
	}

	public async Task NotifyOneAsync(string userId, ServerUpdate update)
	{
		if (!_clients.TryGetValue(userId, out var client)) return;

		try
		{
			await client.WriteAsync(update);
		}
		catch (Exception)
		{
			_clients.Remove(userId, out _);
		}
	}

	public async Task NotifyMultiAsync(IEnumerable<string> userIds, ServerUpdate update)
	{
		await Parallel.ForEachAsync(userIds, async (id, _) => { await NotifyOneAsync(id, update); });
	}
}