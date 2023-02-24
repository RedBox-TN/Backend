using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public sealed class GroupKey : Key
{
	[BsonRepresentation(BsonType.ObjectId)]
	public string? GroupId { get; set; }
}