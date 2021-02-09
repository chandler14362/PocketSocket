using System;
using System.Collections.Generic;
using System.Reflection;
using PocketSocket.Abstractions;
using PocketSocket.Models;

namespace PocketSocket.Implementations
{
    public class MessageRegistry : IMessageRegistry
    {
        private readonly Dictionary<Assembly, List<Type>> _assemblyTypes = new();
        private readonly Dictionary<Assembly, ushort> _assemblyIds = new();
        private readonly Dictionary<Type, uint> _messageIds = new();
        private readonly Dictionary<Type, MessageModel> _messageModels = new();
        private readonly Dictionary<uint, MessageModel> _messageIdToModel = new();
        
        private ushort AddAssembly(Assembly assembly)
        {
            if (_assemblyIds.TryGetValue(assembly, out var assemblyId))
                return assemblyId;
            _assemblyTypes[assembly] = new ();
            return _assemblyIds[assembly] = (ushort)_assemblyIds.Count;
        }

        public void AddMessage(MessageModel message)
        {
            var type = message.MessageType;
            if (_messageIds.ContainsKey(type))
                throw new Exception();
            var assembly = type.Assembly;
            var assemblyId = AddAssembly(assembly);
            var assemblyTypes = _assemblyTypes[assembly];
            var messageId = _messageIds[type] = (uint) ((assemblyId << 16) | (ushort) assemblyTypes.Count);
            _messageModels[type] = message;
            _messageIdToModel[messageId] = message;
            assemblyTypes.Add(type);
        }

        public uint GetMessageId(Type type) => _messageIds[type];

        public bool TryGetMessageId(Type type, out uint id) => _messageIds.TryGetValue(type, out id);

        public MessageModel GetMessageModel(Type type) => _messageModels[type];

        public MessageModel GetMessageModel(uint id) => _messageIdToModel[id];

        public bool TryGetMessageModel(Type type, out MessageModel messageModel)
        {
            messageModel = null;
            return TryGetMessageId(type, out var id) && _messageIdToModel.TryGetValue(id, out messageModel);
        }

        public bool TryGetMessageModel(uint id, out MessageModel messageModel) =>
            _messageIdToModel.TryGetValue(id, out messageModel);
    }
}