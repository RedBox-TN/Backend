using System.Net;

namespace RedBoxAuth.Security_hash_utility;

public interface ISecurityHashUtility
{
	public ulong Calculate(string? userAgent, IPAddress? ipAddress);
	public bool IsValid(ulong savedHash, string? userAgent, IPAddress? ipAddress);
}