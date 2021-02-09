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
    public record SocketInterfaceModel(
        Type InterfaceType,
        IReadOnlyList<SocketInterfaceModel.RequestModel> Requests,
        IReadOnlyList<SocketInterfaceModel.CommandModel> Commands) 
        : TypedSocketModel(InterfaceType)
    {
        public record RequestModel(MethodInfo MethodInfo, Type RequestType, Type ResponseType)
        {
            public static bool TryFromMethodInfo(MethodInfo methodInfo, out RequestModel requestModel)
            {
                requestModel = null;
                var parameters = methodInfo.GetParameters();
                if (parameters.Length != 2)
                    return false;
                var requestType = parameters[1].ParameterType;
                var responseType = methodInfo.ReturnType;
                if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Task<>))
                    responseType = responseType.GetGenericArguments()[0];
                else
                    return false;
                requestModel = new(methodInfo, requestType, responseType);
                return true;
            }
        }

        public record CommandModel(MethodInfo MethodInfo, Type CommandType)
        {
            public static bool TryFromMethodInfo(MethodInfo methodInfo, out CommandModel commandModel)
            {
                commandModel = null;
                var parameters = methodInfo.GetParameters();
                if (parameters.Length != 2)
                    return false;
                var commandType = parameters[1].ParameterType;
                if (methodInfo.ReturnType != typeof(Task))
                    return false;
                commandModel = new(methodInfo, commandType);
                return true;
            }
        }

        public override IEnumerable<MessageModel> GetMessageModels()
        {
            foreach (var requestModel in Requests)
            {
                yield return new(requestModel.RequestType, MessageBehavior.Request);
                yield return new(requestModel.ResponseType, MessageBehavior.Response);
            }

            foreach (var commandModel in Commands)
                yield return new(commandModel.CommandType, MessageBehavior.Command);
        }

        public override bool TypeIsApplicable(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == InterfaceType || type.GetInterfaces().Any(TypeIsApplicable);
        }

        public static SocketInterfaceModel FromInterface(Type interfaceType)
        {
            if (!interfaceType.ImplementsInterface<ISocketInterface>())
                throw new Exception($"Interface {interfaceType.FullName} must implement {nameof(ISocketInterface)}");

            var requests = new List<RequestModel>();
            var commands = new List<CommandModel>();
            var methods = interfaceType.GetMethods();
            foreach (var method in methods)
            {
                if (RequestModel.TryFromMethodInfo(method, out var requestModel))
                    requests.Add(requestModel);
                else if (CommandModel.TryFromMethodInfo(method, out var commandModel))
                    commands.Add(commandModel);
                else
                    throw new Exception($"Interface {interfaceType.Name} contains unhandable method: {method.Name}");
            }

            return new(interfaceType, requests, commands);
        }
    }
}