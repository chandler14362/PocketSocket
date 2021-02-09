using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Models;
using PocketSocket.Models;

namespace PocketSocket.Providers
{
    public static class SocketClientInterfaceTypeProvider
    {
        private static AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("PocketSocket.Dynamic.SocketInterfaceClients"), AssemblyBuilderAccess.Run);

        private static ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule("SocketInterfaceClients");

        private static string GenerateNameFromInterfaceModel(SocketInterfaceModel interfaceModel) =>
            $"{interfaceModel.InterfaceType.Name}Client";

        public static Type FromInterfaceModel(SocketInterfaceModel interfaceModel)
        {
            var typeName = GenerateNameFromInterfaceModel(interfaceModel);
            var typeBuilder = _moduleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(object),
                new[] {interfaceModel.InterfaceType.MakeGenericType(typeof(IPocketSocketClient))});
            foreach (var requestModel in interfaceModel.Requests)
                GenerateRequestMethod(typeBuilder, requestModel);
            foreach (var commandModel in interfaceModel.Commands)
                GenerateCommandMethod(typeBuilder, commandModel);
            return typeBuilder.CreateType();
        }

        private static void GenerateConstructor(TypeBuilder typeBuilder, FieldInfo socketClientField)
        {
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public, 
                CallingConventions.HasThis, 
                Array.Empty<Type>());
            var ilGenerator = constructor.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Array.Empty<Type>()));
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateRequestMethod(
            TypeBuilder typeBuilder, 
            SocketInterfaceModel.RequestModel requestModel)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                requestModel.MethodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(Task<>).MakeGenericType(requestModel.ResponseType),
                new [] { typeof(IPocketSocketClient), requestModel.RequestType });
            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Ldarg_2);
            ilGenerator.Emit(OpCodes.Callvirt, GetPublishMethod(requestModel.RequestType, requestModel.ResponseType));
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateCommandMethod(
            TypeBuilder typeBuilder,
            SocketInterfaceModel.CommandModel commandModel)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                commandModel.MethodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(Task),
                new [] { typeof(IPocketSocketClient), commandModel.CommandType });
            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Ldarg_2);
            ilGenerator.Emit(OpCodes.Callvirt, GetPublishMethod(commandModel.CommandType));
            var completedTaskGetter = typeof(Task).GetProperty("CompletedTask").GetMethod;
            ilGenerator.Emit(OpCodes.Call, completedTaskGetter);
            ilGenerator.Emit(OpCodes.Ret); 
        }

        private static MethodInfo GetPublishMethod(params Type[] genericArguments)
        {
            // For some reason the standard Type.GetMethod(string, int, Type[]) call returns null for our generic publish method
            var methodInfo = typeof(IPocketSocketClient).GetMethods().FirstOrDefault(method =>
                method.IsGenericMethodDefinition &&
                method.Name == "Publish" &&
                method.GetGenericArguments().Length == genericArguments.Length);
            return methodInfo?.MakeGenericMethod(genericArguments);
        }
    }
}