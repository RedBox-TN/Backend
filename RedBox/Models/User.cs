using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBox.Models;

public class User
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string Name { get; set; } = null!;
	public string Surname { get; set; } = null!;
	public string Email { get; set; } = null!;
	public string Username { get; set; } = null!;
	public string Password { get; set; } = null!;
	public string[] PasswordHistory { get; set; } = null!;
	public bool IsBlocked { get; set; } = false;
	public bool IsFaEnable { get; set; } = false;
	public byte[] FaSeed { get; set; } = null!;
	public DateTime LastAccess { get; set; }
	public string[] Chats { get; set; } = null!;
	public string Role { get; set; } = null!;
}