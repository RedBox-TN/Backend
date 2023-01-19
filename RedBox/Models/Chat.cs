using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedBox.Models;

public class Chat
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string Name { get; set; } = null!;
	public string[] Users { get; set; } = null!;
	public DateTime? CreationDate { get; set; }
	public DateTime? DeletedAt { get; set; }
}