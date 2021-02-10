using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PocketSocket.Abstractions;

namespace PocketSocket.Extensions.Hosting
{
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