using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBox.Models;

public sealed class Message
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; } = null!;

	[BsonRepresentation(BsonType.ObjectId)]
	public string? SenderId { get; set; } = null!;

	public byte[]? EncryptedText { get; set; } = null!;
	public byte[]? Iv { get; set; } = null!;

	[BsonIgnoreIfNull] public Attachment? Attachment { get; set; } = null!;

	[BsonRepresentation(BsonType.DateTime)]
	public DateTime? Timestamp { get; set; } = null!;

	public byte DeleteCounter { get; set; } = 0;

	[BsonIgnoreIfDefault] public bool? ToRead { get; set; } = true;
}