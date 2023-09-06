using Castle.DynamicProxy;
using System.Collections.Concurrent;

namespace Mockster;

/// <summary>
/// Defines an interface for intercepting method invocations on a mock object.
/// </summary>
public interface IMocksterInterceptor
{
    /// <summary>
    /// Intercepts a method invocation on a mock object.
    /// </summary>
    /// <param name="invocation">The method invocation information.</param>
    void Intercept(IInvocation invocation);

    /// <summary>
    /// Retrieves the history of intercepted method invocations on a mock object.
    /// </summary>
    /// <returns>A concurrent bag containing the history of method invocations.</returns>
    ConcurrentBag<string> GetInvocationHistory();
}
