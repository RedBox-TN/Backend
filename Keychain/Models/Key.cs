using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public class Key
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public byte[]? Data { get; set; }

	[BsonIgnoreIfNull] public byte[]? Iv { get; set; }

	[BsonRepresentation(BsonType.ObjectId)]
	[BsonIgnoreIfNull]
	public string? UserOwnerId { get; set; }

	[BsonIgnoreIfNull]
	[BsonIgnoreIfDefault]
	public bool? IsEncryptedWithUserPublicKey { get; set; }
}