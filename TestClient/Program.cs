using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Enums;
using PocketSocket;
using TestInterface;

namespace TestClient
{
    public class Program
    {
        public class TestEventHandler : ITestSocketEventHandler
        {
            private readonly IPocketSocketClient _pocketSocketClient;

            public TestEventHandler(IPocketSocketClient pocketSocketClient)
            {
                _pocketSocketClient = pocketSocketClient;
            }

            public Task OnTestEvent(TestEvent @event)
            {
                Console.WriteLine($"Got the event: {@event}");
                return Task.CompletedTask;
            }
        }
        
        public class TestEventHandler2 : ITestSocketEventHandler2
        {
            private readonly IPocketSocketClient _pocketSocketClient;

            public TestEventHandler2(IPocketSocketClient pocketSocketClient)
            {
                _pocketSocketClient = pocketSocketClient;
            }

            public Task OnTestEvent(TestEvent2 @event)
            {
                Console.WriteLine($"Got the event2: {@event}");
                return Task.CompletedTask;
            }
        }

        public class ClientHostedService : IHostedService
        {
            private readonly IPocketSocketClient _pocketSocketClient;
            private readonly ITestSocketInterface<IPocketSocketClient> _testSocketInterface;

            public ClientHostedService(
                IPocketSocketClient pocketSocketClient, 
                ITestSocketInterface<IPocketSocketClient> testSocketInterface)
            {
                _pocketSocketClient = pocketSocketClient;
                _testSocketInterface = testSocketInterface;
            }
            
            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _pocketSocketClient.Start("127.0.0.1", 12245, OnConnectionClosed);
                await _testSocketInterface.DoTestCommand(_pocketSocketClient, new TestCommand("asdasda"));
                
                var stopwatch = new Stopwatch();
                const int requestCount = 2;
                stopwatch.Start();
                var requests = Enumerable.Range(0, requestCount)
                    .Select(i => _testSocketInterface.DoTestRequest(_pocketSocketClient, new(i)));
                await Task.WhenAll(requests);
                stopwatch.Stop();
                Console.WriteLine($"Did {requestCount} requests in {stopwatch.ElapsedMilliseconds} ms");
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await _pocketSocketClient.DisposeAsync();
            }

            private void OnConnectionClosed(IPocketSocketClient pocketSocketClient, ConnectionCloseStatus closeStatus)
            {
                Console.WriteLine($"Connection closed: {closeStatus}");
            }
        }

        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UsePocketSocketClient(builder => CommonBuilder.Build(builder))
                .ConfigureServices(services =>
                    services
                        .AddClientEventHandler<ITestSocketEventHandler, TestEventHandler>()
                        .AddClientEventHandler<ITestSocketEventHandler2, TestEventHandler2>()
                        .AddClientInterface<ITestSocketInterface<IPocketSocketClient>>()
                        .AddHostedService<ClientHostedService>());
    }
}