namespace RedBox.Models;

public class Chat
{
    public string? CollectionName { get; set; }
    public string[] MembersIds { get; set; } = null!;
    public DateTime? CreatedAt { get; set; } = null!;
}