namespace RedBoxAuth.TOTP_utility;

/// <summary>
///     Expose methods that allow 2FA authentication
/// </summary>
public interface ITotpUtility
{
    /// <summary>
    ///     Create shared secret as QrCode usable with authenticator apps
    /// </summary>
    /// <param name="email">Email address of the user</param>
    /// <param name="base64Image">Base64 image of the generated QrCode</param>
    /// <param name="manualCode">Manual code for authenticator apps, to use as an alternative to QrCode</param>
    /// <returns>Byte array containing the shared secret usable in the backend</returns>
    public byte[] CreateSharedSecret(string email, out string? base64Image, out string? manualCode);

    /// <summary>
    ///     Allow to test if an entered code is valid or not
    /// </summary>
    /// <param name="faSeed">Shared secret or the user stored in the database</param>
    /// <param name="code">The code to be verified</param>
    /// <returns>Bool result of the verification</returns>
    public bool VerifyCode(byte[] faSeed, string code);
}