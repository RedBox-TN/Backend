using MongoDB.Driver;
using RedBoxAuth.Authorization;
using RedBoxServices;

namespace RedBox.Services;

[AuthenticationRequired]
public partial class ConversationService : GrpcConversationServices.GrpcConversationServicesBase
{
	private readonly IMongoDatabase _database;
}