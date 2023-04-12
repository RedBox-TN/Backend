namespace RedBoxAuth.TOTP_utility;

public interface ITotpUtility
{
	public byte[] CreateSharedSecret(string email, out string? base64Image, out string? manualCode);
	public bool VerifyCode(byte[] faSeed, string code);
}