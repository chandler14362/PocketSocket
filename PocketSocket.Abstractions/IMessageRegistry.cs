using System;
using PocketSocket.Models;

namespace PocketSocket.Abstractions
{
    public interface IMessageRegistry
    {
        void AddMessage(MessageModel message);
        
        uint GetMessageId(Type type);
        uint GetMessageId<T>() => GetMessageId(typeof(T));
        uint GetMessageId(MessageModel model) => GetMessageId(model.MessageType);
        
        bool TryGetMessageId(Type type, out uint id);
        bool TryGetMessageId<T>(out uint id) => TryGetMessageId(typeof(T), out id);

        MessageModel GetMessageModel(Type type);
        MessageModel GetMessageModel<T>() => GetMessageModel(typeof(T));
        MessageModel GetMessageModel(uint id);

        bool TryGetMessageModel(Type type, out MessageModel messageModel);
        bool TryGetMessageModel(uint id, out MessageModel messageModel);
    }
}