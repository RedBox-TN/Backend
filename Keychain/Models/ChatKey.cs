using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public sealed class ChatKey : Key
{
	[BsonRepresentation(BsonType.ObjectId)]
	public string? ChatCollectionName { get; set; }
}