using Castle.DynamicProxy;
using System.Collections.Concurrent;

namespace Mockster;

/// <summary>
/// Mockster is a utility class for creating and configuring mock objects.
/// </summary>
public static class Mockster
{
    /// <summary>
    /// A proxy generator used for creating mock objects.
    /// </summary>
    private static readonly ProxyGenerator generator = new ProxyGenerator();

    /// <summary>
    /// A concurrent dictionary to map mock objects to their corresponding interceptors.
    /// </summary>
    private static readonly ConcurrentDictionary<object, IMocksterInterceptor> mockToInterceptor = new ConcurrentDictionary<object, IMocksterInterceptor>();

    /// <summary>
    /// Creates a mock object of the specified type T.
    /// </summary>
    /// <typeparam name="T">The type of object to mock.</typeparam>
    /// <returns>A mock object of the specified type.</returns>
    public static T CreateMock<T>() where T : class
    {
        var interceptor = new MocksterInterceptor<T>();
        T mockObject = generator.CreateInterfaceProxyWithoutTarget<T>(interceptor);

        mockToInterceptor[mockObject] = interceptor;

        return mockObject;
    }

    /// <summary>
    /// Retrieves the configuration for a given mock object.
    /// </summary>
    /// <typeparam name="T">The type of the mock object.</typeparam>
    /// <param name="mockObject">The mock object to configure.</param>
    /// <returns>A MocksterInterceptor instance for configuring the mock object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if mockObject is null.</exception>
    /// <exception cref="ArgumentException">Thrown if mockObject is not a Mockster mock object.</exception>
    public static MocksterInterceptor<T> Config<T>(T mockObject) where T : class
    {
        if (mockObject == null)
        {
            throw new ArgumentNullException(nameof(mockObject), "The provided object cannot be null.");
        }

        if (mockToInterceptor.TryGetValue(mockObject, out var interceptor))
        {
            return (MocksterInterceptor<T>)interceptor;
        }
        else
        {
            throw new ArgumentException("The provided object is not a Mockster mock object.");
        }
    }
}
