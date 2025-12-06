using GameCore.Services;
using GameClient.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameClient.Initialization;

/// <summary>
/// Configures and manages the dependency injection container for game services.
/// </summary>
public static class ServiceContainer
{
    /// <summary>
    /// Creates and configures a service provider with all game services.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>A configured service provider.</returns>
    public static IServiceProvider CreateServiceProvider(ILogger logger)
    {
        var services = new ServiceCollection();

        // Register logger
        services.AddSingleton<ILogger>(logger);

        // Register core services with constructor injection
        services.AddSingleton<GameTimeManager>();
        services.AddSingleton<CollisionService>();
        services.AddSingleton<InteractionService>();
        services.AddSingleton<DialogService>(sp => new DialogService(sp.GetRequiredService<ILogger>()));
        services.AddSingleton<CombatService>(sp => new CombatService(sp.GetRequiredService<ILogger>()));

        // Register client services
        services.AddSingleton<EnemyService>(sp => 
            new EnemyService(
                sp.GetRequiredService<ILogger>(),
                sp.GetService<GameCore.State.GameStateService>(),
                itemDatabase: null)); // Item database will be set later in IsoEngineGame
        services.AddSingleton<SaveGameService>();

        // Register as IGameService for lifecycle management
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<GameTimeManager>());
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<CollisionService>());
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<InteractionService>());
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<CombatService>());
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<EnemyService>());
        services.AddSingleton<IGameService>(sp => sp.GetRequiredService<SaveGameService>());

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets all registered game services from the service provider.
    /// </summary>
    public static IEnumerable<IGameService> GetAllGameServices(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetServices<IGameService>();
    }
}

