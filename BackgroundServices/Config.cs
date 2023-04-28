namespace BackgroundServices;

public class Config
{
	public string ExpiredChannelName { get; set; } = "__keyevent@0__:expired";
	public string UsersHashKey { get; set; } = "users";
	public uint ExpiredScanSleepSeconds { get; set; } = 5;
	public uint DandlingScanSleepMinutes { get; set; } = 5;
}