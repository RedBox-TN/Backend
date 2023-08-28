using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBox.Models;

public sealed class Message
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	[BsonRepresentation(BsonType.ObjectId)]
	public string? UserId { get; set; }

	public byte[]? EncryptedText { get; set; } = null!;
	public byte[]? Iv { get; set; } = null!;

	[BsonIgnoreIfNull] public Attachment? Attachment { get; set; } = null!;

	[BsonRepresentation(BsonType.DateTime)]
	public DateTime? Timestamp { get; set; } = null!;
}