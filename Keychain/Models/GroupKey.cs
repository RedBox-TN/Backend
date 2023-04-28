using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Keychain.Models;

public sealed class GroupKey : Key
{
	public string? GroupCollectionName { get; set; }
}