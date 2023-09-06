using Castle.DynamicProxy;

namespace Mockster;

/// <summary>
/// Provides extension methods for the MocksterInterceptor class.
/// </summary>
public static class MocksterExtensions
{
    /// <summary>
    /// Sets up an action to be performed when a specific method is invoked on the mock object.
    /// </summary>
    /// <typeparam name="T">The type of the mock object.</typeparam>
    /// <param name="interceptor">The MocksterInterceptor for the mock object.</param>
    /// <param name="methodName">The name of the method to set up.</param>
    /// <param name="setupAction">The action to be performed when the method is invoked.</param>
    /// <returns>The same MocksterInterceptor instance for chaining.</returns>
    public static MocksterInterceptor<T> SetupMethod<T>(this MocksterInterceptor<T> interceptor,
        string methodName,
        Func<IInvocation, object> setupAction) where T : class
    {
        interceptor.SetupMethod(methodName, setupAction);
        return interceptor;
    }

    /// <summary>
    /// Sets up an action to be performed when a specific method with the expected arguments is invoked on the mock object.
    /// </summary>
    /// <typeparam name="T">The type of the mock object.</typeparam>
    /// <param name="interceptor">The MocksterInterceptor for the mock object.</param>
    /// <param name="methodName">The name of the method to set up.</param>
    /// <param name="expectedArgs">The expected arguments for the method.</param>
    /// <param name="setupAction">The action to be performed when the method is invoked.</param>
    /// <returns>The same MocksterInterceptor instance for chaining.</returns>
    public static MocksterInterceptor<T> SetupMethodWithArgs<T>(this MocksterInterceptor<T> interceptor, string methodName, object[] expectedArgs,
        Func<IInvocation, object> setupAction) where T : class
    {
        interceptor.SetupMethodWithArgs(methodName, expectedArgs, setupAction);
        return interceptor;
    }
}
