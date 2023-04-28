using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public class Key
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public byte[]? Data { get; set; }

	[BsonIgnoreIfNull]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? UserOwnerId { get; set; }

	[BsonIgnoreIfNull]
	public bool? IsEncryptedWithUserKey { get; set; }
}