using System;
using System.Collections.Generic;

namespace GameCore.Services;

/// <summary>
/// Simple service locator pattern implementation for managing game services.
/// Allows registration and resolution of services by interface type.
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly List<IGameService> _gameServices = new();

    /// <summary>
    /// Registers a service instance with the service locator.
    /// </summary>
    /// <typeparam name="T">The interface type to register the service as.</typeparam>
    /// <param name="service">The service instance to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if service is null.</exception>
    public static void Register<T>(T service) where T : class
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        var serviceType = typeof(T);
        _services[serviceType] = service;

        // If the service implements IGameService, add it to the game services list (avoid duplicates)
        if (service is IGameService gameService && !_gameServices.Contains(gameService))
        {
            _gameServices.Add(gameService);
        }
    }

    /// <summary>
    /// Resolves and returns a service instance by interface type.
    /// </summary>
    /// <typeparam name="T">The interface type of the service to resolve.</typeparam>
    /// <returns>The registered service instance, or null if not found.</returns>
    public static T? Get<T>() where T : class
    {
        var serviceType = typeof(T);
        if (_services.TryGetValue(serviceType, out var service))
        {
            return service as T;
        }
        return null;
    }

    /// <summary>
    /// Resolves and returns a service instance by interface type, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The interface type of the service to resolve.</typeparam>
    /// <returns>The registered service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service is not registered.</exception>
    public static T GetRequired<T>() where T : class
    {
        var service = Get<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }
        return service;
    }

    /// <summary>
    /// Checks if a service of the specified type is registered.
    /// </summary>
    /// <typeparam name="T">The interface type to check.</typeparam>
    /// <returns>True if the service is registered, false otherwise.</returns>
    public static bool IsRegistered<T>() where T : class
    {
        return _services.ContainsKey(typeof(T));
    }

    /// <summary>
    /// Gets all registered game services that implement IGameService.
    /// </summary>
    /// <returns>An enumerable of all registered game services.</returns>
    public static IEnumerable<IGameService> GetAllGameServices()
    {
        return _gameServices;
    }

    /// <summary>
    /// Clears all registered services. Useful for testing or resetting state.
    /// </summary>
    public static void Clear()
    {
        _services.Clear();
        _gameServices.Clear();
    }
}

