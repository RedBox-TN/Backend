namespace RedBox.Models;

public sealed class Group : Chat
{
	public string? Name { get; set; } = null!;
	public string[]? AdminsIds { get; set; } = null!;
}