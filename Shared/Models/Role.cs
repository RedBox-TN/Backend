using MemoryPack;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Models;

[MemoryPackable]
public partial class Role
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; } = null!;

	public string Name { get; set; } = null!;

	[MemoryPackIgnore] public string Description { get; set; } = null!;

	public uint Permissions { get; set; }
}