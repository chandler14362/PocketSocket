using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PocketSocket.Abstractions;

namespace PocketSocket.Extensions.Hosting
{
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
}