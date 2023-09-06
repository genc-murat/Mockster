using Castle.DynamicProxy;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Mockster;

public class MocksterInterceptor<T> : IInterceptor, IMocksterInterceptor where T : class
{
    private readonly ConcurrentDictionary<string, Func<IInvocation, object>> _methodBehaviors = new ConcurrentDictionary<string, Func<IInvocation, object>>();
    private readonly ConcurrentDictionary<string, int> _methodCallCounts = new ConcurrentDictionary<string, int>();
    private readonly ConcurrentDictionary<string, object> _propertySetups = new ConcurrentDictionary<string, object>();
    private readonly ConcurrentDictionary<string, Queue<Func<IInvocation, object>>> _methodSequences = new ConcurrentDictionary<string, Queue<Func<IInvocation, object>>>();
    private readonly ConcurrentBag<string> _invocationHistory = new ConcurrentBag<string>();
    private Dictionary<string, object[]> _expectedArgs = new Dictionary<string, object[]>();

    /// <summary>
    /// Gets or sets a function to handle invocations of methods that have not been explicitly set up.
    /// </summary>
    /// <value>
    /// A function taking an <see cref="IInvocation"/> object and returning an object to be used as the return value for the method call.
    /// If this property is null, an exception will be thrown when an unhandled method is invoked.
    /// </value>
    /// <remarks>
    /// This function is invoked when a method that hasn't been set up with either <see cref="SetupMethod"/> or <see cref="SetupMethodWithArgs"/> is called.
    /// It provides a way to define a default behavior for such methods.
    /// </remarks>
    public Func<IInvocation, object> UnhandledMethod { get; set; }

    /// <summary>
    /// Intercepts a method invocation, tracking its call count and executing pre-configured behavior if available.
    /// </summary>
    /// <param name="invocation">The IInvocation object containing details about the method invocation.</param>
    /// <remarks>
    /// This method performs several tasks:
    /// 1. Updates the method call count.
    /// 2. Checks if the method has been set up with expected arguments.
    /// 3. If so, it executes the corresponding setup action.
    /// 4. If not, it checks if the method is part of a pre-configured sequence and executes the next setup action in that sequence if available.
    /// 5. If no specific behavior is configured for the method, it calls HandleUnhandledMethod to handle it.
    /// 6. Records the method invocation history.
    /// </remarks>
    public void Intercept(IInvocation invocation)
    {
        var methodName = GetMethodSignature(invocation);
        var argsKey = string.Join(",", invocation.Arguments.Select(arg => arg == default ? "It.IsAny" : arg.ToString()));
        var fullKey = $"{methodName}|{argsKey}";

        _methodCallCounts.AddOrUpdate(methodName, 1, (_, current) => current + 1);

        object[] expectedArgs = _expectedArgs.ContainsKey(methodName) ? _expectedArgs[methodName] : null;

        if (_methodBehaviors.TryGetValue(fullKey, out var methodSetup) || _methodBehaviors.TryGetValue(methodName, out methodSetup))
        {
            for (int i = 0; i < invocation.Arguments.Length; i++)
            {
                object arg = invocation.Arguments[i];
                object expectedArg = expectedArgs[i];

                if (expectedArg is It<T> itObj)
                {
                    if (!itObj.Matches((T)arg))
                    {
                        return;
                    }
                }
                else if (expectedArg != arg)
                {
                    return;
                }
            }
            HandleMethodSetup(invocation, methodSetup);
        }
        else if (_methodSequences.TryGetValue(fullKey, out var sequence) && sequence.Count > 0)
        {
            var nextSetup = sequence.Dequeue();
            HandleMethodSetup(invocation, nextSetup);
        }
        else
        {
            HandleUnhandledMethod(invocation);
        }
        _invocationHistory.Add($"{invocation.Method.Name} was called with arguments: {string.Join(", ", invocation.Arguments)}");
    }

    /// <summary>
    /// Retrieves the history of method invocations that have been intercepted.
    /// </summary>
    /// <returns>A ConcurrentBag of string representing the history of method invocations.</returns>
    /// <remarks>
    /// Each string in the returned ConcurrentBag provides information about a method invocation, 
    /// including the method name and the arguments with which it was called.
    /// </remarks>
    public ConcurrentBag<string> GetInvocationHistory()
    {
        return _invocationHistory;
    }

    /// <summary>
    /// Sets up a mock method to return a specified value when called.
    /// </summary>
    /// <typeparam name="TResult">The return type of the method being mocked.</typeparam>
    /// <param name="expression">A lambda expression representing the method to be mocked.</param>
    /// <param name="value">The value that should be returned when the method is called.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the method specified by the expression does not exist in the interface, 
    /// or if the return type does not match the specified type.
    /// </exception>
    /// <remarks>
    /// This method allows you to define behavior for a specific method in the interface being mocked, 
    /// specifying the value to be returned when the method is called.
    /// </remarks>
    public void SetupMethod<TResult>(Expression<Func<T, TResult>> expression, TResult value)
    {
        var methodName = GetMethodSignature(expression);
        MethodInfo methodInfo = typeof(T).GetMethod(methodName.Split('(')[0]);

        if (methodInfo == null)
        {
            throw new ArgumentException($"The method {methodName} does not exist in the interface {typeof(T).Name}.");
        }

        if (methodInfo.ReturnType != typeof(TResult))
        {
            throw new ArgumentException($"The return type of method {methodName} is not {typeof(TResult).Name}");
        }
        Func<IInvocation, object> func = invocation => value;
        _methodBehaviors[methodName] = func;
    }

    /// <summary>
    /// Sets up a mock method to execute a specified action when called.
    /// </summary>
    /// <typeparam name="TResult">The return type of the method being mocked.</typeparam>
    /// <param name="expression">A lambda expression representing the method to be mocked.</param>
    /// <param name="setupAction">A function that defines the behavior to be executed when the method is called.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the method specified by the expression does not exist in the interface, 
    /// or if the return type does not match the specified type.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the setup action is null.
    /// </exception>
    /// <remarks>
    /// This method allows you to define behavior for a specific method in the interface being mocked.
    /// The function passed as the setupAction parameter will be invoked when the method is called, 
    /// and its return value will be used as the return value of the mocked method.
    /// </remarks>
    public void SetupMethod<TResult>(Expression<Func<T, TResult>> expression, Func<IInvocation, TResult> setupAction)
    {
        var methodName = GetMethodSignature(expression);
        MethodInfo methodInfo = typeof(T).GetMethod(methodName.Split('(')[0]);

        if (methodInfo == null)
        {
            throw new ArgumentException($"The method {methodName} does not exist in the interface {typeof(T).Name}.");
        }

        if (methodInfo.ReturnType != typeof(TResult))
        {
            throw new ArgumentException($"The return type of method {methodName} is not {typeof(TResult).Name}");
        }

        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction), "The setup action cannot be null.");
        }

        Func<IInvocation, object> func = invocation => setupAction(invocation);
        _methodBehaviors[methodName] = func;

    }

    /// <summary>
    /// Sets up a sequence of return values for a specific mocked method with expected arguments.
    /// </summary>
    /// <typeparam name="TResult">The return type of the method being mocked.</typeparam>
    /// <param name="expression">A lambda expression representing the method to be mocked.</param>
    /// <param name="results">An IEnumerable of values that the mocked method should return in sequence.</param>
    /// <param name="expectedArgs">The expected arguments that the method should be called with.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if there are no more results available in the sequence when the method is invoked.
    /// </exception>
    /// <remarks>
    /// This method allows you to set up a sequence of return values for a specific method. When the method is called, 
    /// it will return the next value from the sequence of provided results. Once all values from the sequence have been returned, 
    /// an InvalidOperationException will be thrown for subsequent calls.
    /// </remarks>
    public void SetupSequence<TResult>(Expression<Func<T, TResult>> expression, IEnumerable<TResult> results, params object[] expectedArgs)
    {
        var methodName = GetMethodSignature(expression);

        var argsKey = string.Join(",", expectedArgs.Select(arg => arg?.ToString() ?? "It.IsAny"));
        var fullKey = $"{methodName}|{argsKey}";

        var resultQueue = new Queue<TResult>(results);

        Func<IInvocation, object> sequenceFunc = invocation =>
        {
            if (resultQueue.Count > 0)
            {
                return resultQueue.Dequeue();
            }
            else
            {
                throw new InvalidOperationException($"No more results available for {methodName}.");
            }
        };

        _methodSequences[fullKey] = new Queue<Func<IInvocation, object>>(new[] { sequenceFunc });
    }

    /// <summary>
    /// Sets up a mock property to return a specific value when accessed.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being mocked.</typeparam>
    /// <param name="expression">A lambda expression representing the property to be mocked.</param>
    /// <param name="value">The value that should be returned when the property is accessed.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the expression does not represent a property.
    /// </exception>
    /// <remarks>
    /// This method allows you to define behavior for a specific property in the interface being mocked. 
    /// The value passed in will be returned when the property is accessed.
    /// </remarks>
    public void SetupProperty<TProperty>(Expression<Func<T, TProperty>> expression, TProperty value)
    {
        if (expression.Body is MemberExpression memberExpression &&
            memberExpression.Member.MemberType == MemberTypes.Property)
        {
            var propertyName = memberExpression.Member.Name;
            _propertySetups[propertyName] = value;
        }
        else
        {
            throw new ArgumentException("Invalid expression. Expression should represent a property.");
        }
    }

    /// <summary>
    /// Sets up a mock method to return a specified value when called.
    /// </summary>
    /// <typeparam name="TResult">The return type of the method being mocked.</typeparam>
    /// <param name="expression">A lambda expression representing the method to be mocked.</param>
    /// <param name="value">The value that should be returned when the method is called.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the method specified by the expression does not exist in the interface, 
    /// or if the return type does not match the specified type.
    /// </exception>
    /// <remarks>
    /// This method allows you to define behavior for a specific method in the interface being mocked, 
    /// specifying the value to be returned when the method is called.
    /// </remarks>
    public void SetupMethodReturns<TResult>(Expression<Func<T, TResult>> expression, TResult value)
    {
        var methodName = GetMethodSignature(expression);
        MethodInfo methodInfo = typeof(T).GetMethod(methodName.Split('(')[0]);

        if (methodInfo == null)
        {
            throw new ArgumentException($"The method {methodName} does not exist in the interface {typeof(T).Name}.");
        }

        if (methodInfo.ReturnType != typeof(TResult))
        {
            throw new ArgumentException($"The return type of method {methodName} is not {typeof(TResult).Name}");
        }

        Func<IInvocation, object> func = invocation => value;
        _methodBehaviors[methodName] = func;
    }

    /// <summary>
    /// Sets up a mock method with specific expected arguments and a corresponding action to take when the method is called.
    /// </summary>
    /// <param name="expression">The lambda expression representing the method to be mocked.</param>
    /// <param name="expectedArgs">The expected arguments that the method should be called with.</param>
    /// <param name="setupAction">The action to be executed when the method is called with the expected arguments.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the method specified by the expression does not exist in the interface,
    /// or if the number of expected arguments does not match the number of method parameters.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown if the setup action is null.</exception>
    public void SetupMethodWithArgs(Expression<Func<T, object>> expression, object[] expectedArgs, Func<IInvocation, object> setupAction)
    {
        var methodName = GetMethodSignature(expression);
        MethodInfo methodInfo = typeof(T).GetMethod(methodName.Split('(')[0]);

        _expectedArgs[methodName] = expectedArgs;

        if (methodInfo == null)
        {
            throw new ArgumentException($"The method {methodName} does not exist in the interface {typeof(T).Name}.");
        }

        var parameterInfos = methodInfo.GetParameters();
        if (parameterInfos.Length != expectedArgs.Length)
        {
            throw new ArgumentException("The number of expected arguments does not match the number of method parameters.");
        }

        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction), "The setup action cannot be null.");
        }

        var keyArgs = expectedArgs.Select(arg =>
        {
            if (arg is It<T> itObj)
                return "It.IsAny";
            else
                return arg.ToString();
        }).ToArray();

        string key = $"{methodName}|{string.Join(",", keyArgs)}";
        _methodBehaviors[key] = setupAction;
    }

    /// <summary>
    /// Verifies that a specific method represented by a lambda expression has been called a given number of times.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the method.</typeparam>
    /// <param name="expression">The lambda expression representing the method call to verify.</param>
    /// <param name="expectedCount">The expected number of times the method should have been called.</param>
    /// <exception cref="InvalidOperationException">Thrown if the actual number of calls does not match the expected count.</exception>
    public void VerifyMethodCallCount<TResult>(Expression<Func<T, TResult>> expression, int expectedCount)
    {
        var methodName = GetMethodSignature(expression);
        int actualCount = GetMethodCallCount(methodName);

        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException($"Method {methodName} was expected to be called {expectedCount} times but was actually called {actualCount} times.");
        }
    }

    /// <summary>
    /// Constructs a method signature string based on an IInvocation instance.
    /// </summary>
    /// <param name="invocation">The IInvocation instance containing information about the method call.</param>
    /// <returns>A string representing the method's signature, including parameter and generic types.</returns>
    private static string GetMethodSignature(IInvocation invocation)
    {
        var paramTypes = string.Join(",", invocation.Method.GetParameters().Select(p =>
            $"{(p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : "")}{p.ParameterType.FullName}"));
        var genericTypes = string.Join(",", invocation.Method.GetGenericArguments().Select(g => g.FullName));
        return $"{invocation.Method.Name}({paramTypes})<{genericTypes}>";
    }

    /// <summary>
    /// Constructs a method signature string based on a lambda expression of type Expression<Func<T, object>>.
    /// </summary>
    /// <param name="expression">The lambda expression representing the method call.</param>
    /// <returns>A string representing the method's signature, including parameter types.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided expression does not represent a method call.</exception>
    private static string GetMethodSignature(Expression<Func<T, object>> expression)
    {
        if (expression.Body is MethodCallExpression methodCall)
        {
            var paramTypes = string.Join(",", methodCall.Method.GetParameters().Select(p => p.ParameterType.Name));
            return $"{methodCall.Method.Name}({paramTypes})";
        }
        else if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MethodCallExpression operandMethodCall)
        {
            var paramTypes = string.Join(",", operandMethodCall.Method.GetParameters().Select(p => p.ParameterType.Name));
            return $"{operandMethodCall.Method.Name}({paramTypes})";
        }
        throw new ArgumentException("Invalid expression. Expression should represent a method call.");
    }

    /// <summary>
    /// Constructs a method signature string based on a lambda expression of type Expression<Func<T, TResult>>.
    /// </summary>
    /// <param name="expression">The lambda expression representing the method call.</param>
    /// <returns>A string representing the method's signature, including parameter types.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided expression does not represent a method call.</exception>
    private static string GetMethodSignature<TResult>(Expression<Func<T, TResult>> expression)
    {
        if (expression.Body is MethodCallExpression methodCall)
        {
            var paramTypes = string.Join(",", methodCall.Method.GetParameters().Select(p => p.ParameterType.Name));
            return $"{methodCall.Method.Name}({paramTypes})";
        }
        throw new ArgumentException("Invalid expression. Expression should represent a method call.");
    }

    /// <summary>
    /// Handles the setup action for a method invocation and sets the return value.
    /// </summary>
    /// <param name="invocation">The method invocation information.</param>
    /// <param name="setupAction">The action that provides the return value or performs other setups.</param>
    private void HandleMethodSetup(IInvocation invocation, Func<IInvocation, object> setupAction)
    {
        var result = setupAction(invocation);
        invocation.ReturnValue = HandleReturnType(result, invocation.Method.ReturnType);
    }

    /// <summary>
    /// Converts the result to match the expected return type of a method.
    /// Handles special types like Task and ValueTask.
    /// </summary>
    /// <param name="result">The raw result returned from the setup action.</param>
    /// <param name="returnType">The expected return type of the method.</param>
    /// <returns>The processed result that matches the expected return type.</returns>
    private object HandleReturnType(object result, Type returnType)
    {
        if (result is Task taskResult)
        {
            return taskResult;
        }
        if (result is ValueTask valueTaskResult)
        {
            return valueTaskResult;
        }

        if (returnType.IsGenericType)
        {
            var genericType = returnType.GetGenericTypeDefinition();
            var genericTypeArgument = returnType.GenericTypeArguments[0];

            if (genericType == typeof(Task<>))
            {
                return CreateGenericTask(result, genericTypeArgument);
            }
            if (genericType == typeof(ValueTask<>))
            {
                return CreateGenericValueTask(result, genericTypeArgument);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a Task of a specific generic type, containing a converted result.
    /// </summary>
    /// <param name="result">The result to be stored in the Task.</param>
    /// <param name="genericTypeArgument">The generic type argument for the Task.</param>
    /// <returns>A Task of type Task<T>, where T is the generic type argument.</returns>
    private Task CreateGenericTask(object result, Type genericTypeArgument)
    {
        var convertedResult = Convert.ChangeType(result, genericTypeArgument);
        return (Task)typeof(Task).GetMethod("FromResult").MakeGenericMethod(genericTypeArgument).Invoke(null, new[] { convertedResult });
    }

    /// <summary>
    /// Creates a ValueTask of object type, containing a converted result.
    /// </summary>
    /// <param name="result">The result to be stored in the ValueTask.</param>
    /// <param name="genericTypeArgument">The original type of the result before conversion.</param>
    /// <returns>A ValueTask containing the converted result.</returns>
    private ValueTask<object> CreateGenericValueTask(object result, Type genericTypeArgument)
    {
        var convertedResult = Convert.ChangeType(result, genericTypeArgument);
        return new ValueTask<object>(convertedResult);
    }

    /// <summary>
    /// Handles method invocations that have not been set up in the mock.
    /// </summary>
    /// <param name="invocation">The method invocation information.</param>
    /// <exception cref="NotImplementedException">Thrown if no setup for the method exists and UnhandledMethod is null.</exception>
    private void HandleUnhandledMethod(IInvocation invocation)
    {
        if (UnhandledMethod != null)
        {
            invocation.ReturnValue = UnhandledMethod(invocation);
        }
        else
        {
            throw new NotImplementedException($"No mock setup for method: {invocation.Method.Name}");
        }
    }

    /// <summary>
    /// Retrieves the number of times a specific method has been called.
    /// </summary>
    /// <param name="methodName">The name of the method to query.</param>
    /// <returns>The number of times the method has been called. Returns 0 if the method has not been called.</returns>
    private int GetMethodCallCount(string methodName)
    {
        return _methodCallCounts.TryGetValue(methodName, out var count) ? count : 0;
    }
}