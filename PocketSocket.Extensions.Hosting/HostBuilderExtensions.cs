using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketSocket.Abstractions;
using PocketSocket.Implementations;

namespace PocketSocket
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UsePocketSocketServer(
            this IHostBuilder hostBuilder, 
            Action<IPocketSocketBuilder> onBuild) =>
            hostBuilder.ConfigureServices((_, services) =>
            {
                IPocketSocketBuilder builder = new PocketSocketBuilder();
                onBuild(builder);
                var server = builder.BuildServer();
                services.AddSingleton(server);
                services.AddSingleton<IPocketSocketServer>(server);
                services.AddHostedService<SocketInterfaceImplementationBinder>();
            });
        
        public static IHostBuilder UsePocketSocketServer<T>(
            this IHostBuilder hostBuilder, 
            Action<IPocketSocketBuilder> onBuild)
            where T: class, ISocketConnection =>
            hostBuilder.ConfigureServices((_, services) =>
            {
                var builder = new PocketSocketBuilder();
                onBuild(builder);
                var server = builder.BuildServer<T>();
                services.AddSingleton(server);
                services.AddSingleton<IPocketSocketServer>(server);
                services.AddHostedService<SocketInterfaceImplementationBinder>();
            });

        public static IHostBuilder UsePocketSocketServer(
            this IHostBuilder hostBuilder, 
            Action<HostBuilderContext, IPocketSocketBuilder> onBuild) =>
            hostBuilder.ConfigureServices((hostBuilderContext, services) =>
            {
                IPocketSocketBuilder builder = new PocketSocketBuilder();
                onBuild(hostBuilderContext, builder);
                var server = builder.BuildServer();
                services.AddSingleton(server);
                services.AddSingleton<IPocketSocketServer>(server);
                services.AddHostedService<SocketInterfaceImplementationBinder>();
            });
        
        public static IHostBuilder UsePocketSocketServer<T>(
            this IHostBuilder hostBuilder, 
            Action<HostBuilderContext, IPocketSocketBuilder> onBuild)
            where T: class, ISocketConnection =>
            hostBuilder.ConfigureServices((hostBuilderContext, services) =>
            {
                var builder = new PocketSocketBuilder();
                onBuild(hostBuilderContext, builder);
                var server = builder.BuildServer<T>();
                services.AddSingleton(server);
                services.AddSingleton<IPocketSocketServer>(server);
                services.AddHostedService<SocketInterfaceImplementationBinder>();
            });
        
        public static IHostBuilder UsePocketSocketClient(
            this IHostBuilder hostBuilder,
            Action<IPocketSocketBuilder> onBuild) =>
            hostBuilder.ConfigureServices((_, services) =>
            {
                var builder = new PocketSocketBuilder();
                onBuild(builder);
                var client = builder.BuildClient();
                services.AddSingleton(client);
                services.AddHostedService<SocketEventHandlerImplementationBinder>();
            });

        public static IHostBuilder UsePocketSocketClient(
            IHostBuilder hostBuilder,
            Action<HostBuilderContext, IPocketSocketBuilder> onBuild) =>
            hostBuilder.ConfigureServices((hostBuilderContext, services) =>
            {
                var builder = new PocketSocketBuilder();
                onBuild(hostBuilderContext, builder);
                var client = builder.BuildClient();
                services.AddSingleton(client);
                services.AddHostedService<SocketEventHandlerImplementationBinder>();
            });
    }
    
    public class SocketInterfaceImplementationBinder : IHostedService
    {
        private readonly IPocketSocketServer _server;
        private readonly IReadOnlyList<ISocketInterface> _socketInterfaces;

        public SocketInterfaceImplementationBinder(
            IPocketSocketServer server, 
            IEnumerable<ISocketInterface> socketInterfaces)
        {
            _server = server;
            _socketInterfaces = socketInterfaces.ToList();
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var socketInterface in _socketInterfaces)
                _server.Bind(socketInterface);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var socketInterface in _socketInterfaces)
                _server.Unbind(socketInterface);
            return Task.CompletedTask;
        }
    }

    public class SocketEventHandlerImplementationBinder : IHostedService
    {
        private readonly IPocketSocketClient _client;
        private readonly IReadOnlyList<ISocketEventHandler> _eventHandlers;

        public SocketEventHandlerImplementationBinder(
            IPocketSocketClient client,
            IEnumerable<ISocketEventHandler> eventHandlers)
        {
            _client = client;
            _eventHandlers = eventHandlers.ToArray();
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var eventHandler in _eventHandlers)
                _client.Bind(eventHandler);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var eventHandler in _eventHandlers)
                _client.Unbind(eventHandler);
            return Task.CompletedTask;
        }
    }
}