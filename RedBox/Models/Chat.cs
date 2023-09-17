using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBox.Models;

public class Chat
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; } = null!;

	[BsonRepresentation(BsonType.ObjectId)]
	public string[]? MembersIds { get; set; } = null!;

	[BsonRepresentation(BsonType.DateTime)]
	public DateTime CreatedAt { get; set; }
}