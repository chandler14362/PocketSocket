using System;
using PocketSocket.Abstractions.Enums;

namespace PocketSocket.Models
{
    public record MessageModel(Type MessageType, MessageBehavior Behavior);
}