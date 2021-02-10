using System;
using PocketSocket.Abstractions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddClientInterface<T>(this IServiceCollection services) 
            where T : class, ISocketInterface<IPocketSocketClient> =>
            services.AddSingleton(serviceProvider =>
            {
                var pocketSocketClient = serviceProvider.GetService<IPocketSocketClient>();
                if (pocketSocketClient is null)
                    throw new Exception("No PocketSocketClient ready for interfacing");
                return pocketSocketClient.Open<T>();
            });

        public static IServiceCollection AddClientEventHandler<TService, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
            where TService : class, ISocketEventHandler
            where TImplementation : class, TService
        {
            if (serviceLifetime is not ServiceLifetime.Singleton) throw new NotImplementedException();
            return services
                .AddSingleton<TService, TImplementation>()
                .AddSingleton<ISocketEventHandler>(serviceProvider => serviceProvider.GetService<TService>());
        }

        public static IServiceCollection AddServerInterface<TService, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
            where TService : class, ISocketInterface
            where TImplementation : class, TService
        {
            if (serviceLifetime is not ServiceLifetime.Singleton) throw new NotImplementedException();
            return services
                .AddSingleton<TService, TImplementation>()
                .AddSingleton<ISocketInterface>(serviceProvider => serviceProvider.GetService<TService>());
        }
    }
}