using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBox.Models;

public class Message
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	[BsonRepresentation(BsonType.ObjectId)]
	public string? UserId { get; set; }

	public byte[]? Text { get; set; } = null!;

	public Attachment Attachment { get; set; } = null!;
	public DateTime? Timestamp { get; set; }

	public DateTime? DeletedAt { get; set; }
}