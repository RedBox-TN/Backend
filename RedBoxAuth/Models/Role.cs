using MemoryPack;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBoxAuth.Models;

[MemoryPackable]
public partial class Role
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; } = null!;

	public string Name { get; set; } = null!;
	public ushort Permissions { get; set; }
}