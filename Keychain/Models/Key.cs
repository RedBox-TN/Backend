using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public class Key
{
	[BsonId]
	[BsonRequired]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	[BsonRequired] public byte[]? Data { get; set; }

	[BsonRequired]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? UserId { get; set; }

	[BsonIgnore] public KeyType? KeyType { get; set; }
}