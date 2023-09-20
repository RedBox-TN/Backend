using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RedBox.Models;
using RedBox.Providers;
using RedBox.Settings;
using RedBoxAuth.Authorization;
using RedBoxServices;
using Shared;
using Shared.Models;
using Status = Shared.Status;

namespace RedBox.Services;

public class SupervisedChatService : GrpcSupervisedChatService.GrpcSupervisedChatServiceBase
{
    private readonly RedBoxApplicationSettings _appSettings;
    private readonly IClientsRegistryProvider _clientsRegistry;
    private readonly RedBoxDatabaseSettings _dbSettings;
    private readonly IMongoClient _mongoClient;

    public SupervisedChatService(IOptions<RedBoxDatabaseSettings> dbSettings,
        IOptions<RedBoxApplicationSettings> appSettings, IClientsRegistryProvider clientsRegistry)
    {
        _clientsRegistry = clientsRegistry;
        _dbSettings = dbSettings.Value;
        _appSettings = appSettings.Value;

        _mongoClient = new MongoClient(_dbSettings.ConnectionString);
    }

    [PermissionsRequired(DefaultPermissions.ReadOtherUsersChats)]
    public override async Task<ChatResponse> GetSupervisedChat(IdMessage request, ServerCallContext context)
    {
        var db = _mongoClient.GetDatabase(_dbSettings.DatabaseName)
            .GetCollection<GrpcChat>(_dbSettings.ChatDetailsCollection);

        if (string.IsNullOrEmpty(request.Id))
            return new ChatResponse
            {
                Result = new Result
                {
                    Status = Status.MissingParameters
                }
            };

        var chat = await db.Find(c => c.Id == request.Id).FirstOrDefaultAsync();

        return new ChatResponse
        {
            Chat = new GrpcChat
            {
                Id = chat.Id,
                CreatedAt = chat.CreatedAt,
                Members = { chat.Members },
                Messages = { await GetChatMessagesAsync(chat.Id) }
            },
            Result = new Result
            {
                Status = Status.Ok
            }
        };
    }

    [PermissionsRequired(DefaultPermissions.DeleteSupervisedChat)]
    public override async Task<Result> DeleteSupervisedChat(IdMessage request, ServerCallContext context)
    {
        var chatDb = _mongoClient.GetDatabase(_dbSettings.DatabaseName)
            .GetCollection<GrpcChat>(_dbSettings.ChatDetailsCollection);

        var result = await chatDb.Find(c => c.Id == request.Id).FirstOrDefaultAsync();

        // problema, manca il campo delete, quindi non posso controllarlo

        await chatDb.DeleteOneAsync(c => c.Id == request.Id);

        var messageDb = _mongoClient.GetDatabase(_dbSettings.DatabaseName)
            .GetCollection<Message>(result.Id);

        return new Result
        {
            Status = Status.Ok
        };
    }

    private async Task<GrpcMessage[]> GetChatMessagesAsync(string chatId, int chunk = 0)
    {
        var found = await _mongoClient.GetDatabase(_dbSettings.ChatsDatabase).GetCollection<Message>(chatId)
            .Find(m => !m.UserDeleted).SortByDescending(m => m.Timestamp)
            .Skip(chunk * _appSettings.MsgRetrieveChunkSize)
            .Limit(_appSettings.MsgRetrieveChunkSize).ToListAsync();

        // TODO manca il check del deleted (se cancellato non è da fetchare)
        var messages = new GrpcMessage[found.Count];
        for (var i = 0; i < found.Count; i++)
            messages[i] = new GrpcMessage
            {
                Id = found[i].Id,
                Timestamp = Timestamp.FromDateTime(found[i].Timestamp),
                EncryptedText = ByteString.CopyFrom(found[i].EncryptedText),
                Iv = ByteString.CopyFrom(found[i].Iv),
                SenderId = found[i].SenderId,
                Attachments = { ToGrpcAttachments(found[i].Attachments) }
            };

        return messages;
    }

    private GrpcAttachment[] ToGrpcAttachments(Attachment[]? attachments)
    {
        if (attachments is null) return Array.Empty<GrpcAttachment>();

        var result = new GrpcAttachment[attachments.Length];
        for (var i = 0; i < attachments.Length; i++)
            result[i] = new GrpcAttachment
            {
                Id = attachments[i].Id,
                Name = attachments[i].Name
            };

        return result;
    }
}