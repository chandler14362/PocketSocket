using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketSocket;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Enums;
using PocketSocket.Implementations;
using TestInterface;

namespace TestServer
{
    class Program
    {
        public class TestInterface : ITestSocketInterface<CustomConnection>
        {
            private readonly IPocketSocketServer _pocketSocketServer;
            
            public TestInterface(IPocketSocketServer pocketSocketServer)
            {
                _pocketSocketServer = pocketSocketServer;
            }
            
            public Task<TestResponse> DoTestRequest(CustomConnection connection, TestRequest request)
            {
                return Task.FromResult(new TestResponse(request.X));
            }

            public Task DoTestCommand(CustomConnection connection, TestCommand command)
            {
                _pocketSocketServer.Write(connection, new TestEvent());
                //_pocketSocketServer.DisposeConnection(connection);
                return Task.CompletedTask;
            }
        }

        public class CustomConnection : BaseSocketConnection
        {
            public CustomConnection(IStreamReader streamReader, IStreamWriter streamWriter) : base(streamReader, streamWriter)
            {
            }
        }
        
        public class ServerHostedService : IHostedService
        {
            private readonly IPocketSocketServer<CustomConnection> _pocketSocketServer;

            public ServerHostedService(IPocketSocketServer<CustomConnection> pocketSocketServer)
            {
                _pocketSocketServer = pocketSocketServer;
            }
            
            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _pocketSocketServer.Start(IPAddress.Any, 12245, ConnectionFactory, OnConnectionComplete);
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await _pocketSocketServer.DisposeAsync();
            }

            public void OnConnectionComplete(CustomConnection connection, ConnectionCloseStatus closeStatus)
            {
                Console.WriteLine($"Connection complete: {closeStatus}");
            }
            
            private static CustomConnection ConnectionFactory(IStreamReader streamReader, IStreamWriter streamWriter)
            {
                return new CustomConnection(streamReader, streamWriter);
            }
        }

        static void Main(string[] args) => CreateHostBuilder(args).Build().Run();
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UsePocketSocketServer<CustomConnection>(builder => CommonBuilder.Build(builder))
                .ConfigureServices(services =>
                    services
                        .AddServerInterface<ITestSocketInterface<CustomConnection>, TestInterface>()
                        .AddHostedService<ServerHostedService>());
    }
}