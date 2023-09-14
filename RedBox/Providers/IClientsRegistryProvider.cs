using Grpc.Core;
using RedBoxServices;

namespace RedBox.Providers;

public interface IClientsRegistryProvider
{
	public void Add(string userId, IServerStreamWriter<ServerUpdate> connection);
	public bool TryToGet(string userId, out IServerStreamWriter<ServerUpdate>? client);
	public void Remove(string userId);
	public Task NotifyOneAsync(string userId, ServerUpdate update);
	public Task NotifyMultiAsync(IEnumerable<string> userIds, ServerUpdate update);
}