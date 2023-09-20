namespace RedBox.Settings;

public class RedBoxApplicationSettings
{
    public int PasswordHistorySize { get; set; } = 3;
    public int MaxMessageSizeMb { get; set; } = 4;
    public int MaxAttachmentSizeMb { get; set; } = 256;
    public int MaxAttachmentsPerMsg { get; set; } = 4;
    public int MsgRetrieveChunkSize { get; set; } = 20;
}