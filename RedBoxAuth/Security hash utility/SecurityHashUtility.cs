using System.Net;
using Standart.Hash.xxHash;

namespace RedBoxAuth.Security_hash_utility;

/// <inheritdoc />
public class SecurityHashUtility : ISecurityHashUtility
{
	/// <inheritdoc />
	public ulong Calculate(string? userAgent, string? ipAddress)
	{
		return xxHash64.ComputeHash(userAgent + ipAddress);
	}

	/// <inheritdoc />
	public bool IsValid(ulong savedHash, string? userAgent, string? ipAddress)
	{
		return savedHash == Calculate(userAgent, ipAddress);
	}
}