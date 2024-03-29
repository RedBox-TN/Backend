using System.Security.Cryptography;
using Google.Authenticator;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;

namespace RedBoxAuth.TOTP_utility;

/// <inheritdoc />
public class TotpUtility(IOptions<AuthSettings> authOptions) : ITotpUtility
{
	private readonly AuthSettings _authOptions = authOptions.Value;

#pragma warning disable CS1591

	/// <inheritdoc />
	public bool VerifyCode(byte[] faSeed, string code)
	{
		var tfa = new TwoFactorAuthenticator();
		return tfa.ValidateTwoFactorPIN(faSeed, code);
	}

	public byte[] CreateSharedSecret(string email, out string? base64Image, out string? manualCode)
#pragma warning restore CS1591
	{
		var key = RandomNumberGenerator.GetBytes(_authOptions.TotpSharedSecretSize);
		var tfa = new TwoFactorAuthenticator();
		var setupInfo = tfa.GenerateSetupCode(_authOptions.TotpIssuer, email, key);
		base64Image = setupInfo.QrCodeSetupImageUrl;
		manualCode = setupInfo.ManualEntryKey;
		return key;
	}
}