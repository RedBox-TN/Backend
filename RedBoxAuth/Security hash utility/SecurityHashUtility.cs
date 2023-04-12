using System.Net;
using Standart.Hash.xxHash;

namespace RedBoxAuth.Security_hash_utility;

public class SecurityHashUtility : ISecurityHashUtility
{
	public ulong Calculate(string? userAgent, IPAddress? ipAddress)
	{
		return xxHash64.ComputeHash(userAgent + ipAddress);
	}

	public bool IsValid(ulong savedHash, string? userAgent, IPAddress? ipAddress)
	{
		return savedHash == Calculate(userAgent, ipAddress);
	}
}