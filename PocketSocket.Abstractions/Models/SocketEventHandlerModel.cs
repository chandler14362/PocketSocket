using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PocketSocket.Abstractions.Enums;
using PocketSocket.Abstractions.Extensions;
using PocketSocket.Models;

namespace PocketSocket.Abstractions.Models
{
    public record SocketEventHandlerModel(
        Type InterfaceType, 
        IReadOnlyList<SocketEventHandlerModel.EventModel> Events)
        : TypedSocketModel(InterfaceType)
    {
        public record EventModel(MethodInfo MethodInfo, Type EventType)
        {
            public static bool TryFromMethodInfo(MethodInfo methodInfo, out EventModel eventModel)
            {
                eventModel = null;
                var parameters = methodInfo.GetParameters();
                if (parameters.Length != 1)
                    return false;
                var eventType = parameters[0].ParameterType;
                if (methodInfo.ReturnType != typeof(Task))
                    return false;
                eventModel = new(methodInfo, eventType);
                return true;
            }
        }

        public override IEnumerable<MessageModel> GetMessageModels() => 
            Events.Select(eventModel => new MessageModel(eventModel.EventType, MessageBehavior.Event));

        public static SocketEventHandlerModel FromInterface(Type interfaceType)
        {
            if (!interfaceType.ImplementsInterface<ISocketEventHandler>())
                throw new Exception($"Interface {interfaceType.FullName} must implement {nameof(ISocketInterface)}");
            
            var eventModels = new List<EventModel>();
            var methods = interfaceType.GetMethods();
            foreach (var method in methods)
            {
                if (!EventModel.TryFromMethodInfo(method, out var eventModel))
                    throw new Exception($"Unable to handle method {method} in interface {interfaceType.Name}");
                eventModels.Add(eventModel);
            }

            return new(interfaceType, eventModels);
        }
    }
}