using System.Net;

namespace RedBoxAuth.Security_hash_utility;

/// <summary>
///     Expose methods for security hash which is and added layer of security
/// </summary>
public interface ISecurityHashUtility
{
    /// <summary>
    ///     Calculate the hash from the user agent and the ip address of the client
    /// </summary>
    /// <param name="userAgent">The user agent string of the client</param>
    /// <param name="ipAddress">The i[ address of the client</param>
    /// <returns>Calculated hash as ulong</returns>
    public ulong Calculate(string? userAgent, IPAddress? ipAddress);

    /// <summary>
    ///     Check if the parameters produces the same hash as the stored one
    /// </summary>
    /// <param name="savedHash">Stored hash</param>
    /// <param name="userAgent">Current User agent</param>
    /// <param name="ipAddress">Current ip</param>
    /// <returns>Bool result of the check</returns>
    public bool IsValid(ulong savedHash, string? userAgent, IPAddress? ipAddress);
}