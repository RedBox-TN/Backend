using MemoryPack;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Models;

[MemoryPackable]
public partial class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string Surname { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    [MemoryPackIgnore] public byte[] PasswordHash { get; set; } = null!;
    [MemoryPackIgnore] public byte[] Salt { get; set; } = null!;
    [MemoryPackIgnore] public List<(byte[] Password, byte[] Salt)>? PasswordHistory { get; set; } = null!;
    [MemoryPackIgnore] public byte InvalidLoginAttempts { get; set; }
    [MemoryPackIgnore] public bool IsBlocked { get; set; } = false;
    public bool IsFaEnable { get; set; } = false;
    public byte[]? FaSeed { get; set; } = null!;
    [BsonIgnore] public ulong SecurityHash { get; set; }
    [MemoryPackIgnore] public DateTime LastAccess { get; set; }

    [BsonRepresentation(BsonType.String)] public string[] ChatIds { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string[] GroupIds { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    [MemoryPackIgnore]
    public string RoleId { get; set; } = null!;

    [BsonIgnore] public Role Role { get; set; } = null!;
    public string Name { get; set; } = null!;
    [BsonIgnore] public bool IsAuthenticated { get; set; }
    public string Biography { get; set; } = null!;
    [BsonIgnoreIfDefault] public bool NeedsProvisioning { get; set; } = false;
}