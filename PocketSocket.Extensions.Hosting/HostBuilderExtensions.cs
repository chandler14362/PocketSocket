using System;
using Microsoft.Extensions.DependencyInjection;
using PocketSocket.Abstractions;
using PocketSocket.Extensions.Hosting;
using PocketSocket.Implementations;

namespace Microsoft.Extensions.Hosting
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
}