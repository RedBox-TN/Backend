using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public sealed class ExecutiveKey : Key
{
	[BsonRepresentation(BsonType.ObjectId)]
	public string? ChatId { get; set; }
}