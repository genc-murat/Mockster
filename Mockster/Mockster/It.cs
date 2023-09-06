namespace Mockster;

/// <summary>
/// Provides utility methods for defining matchers and values for mock object invocations.
/// </summary>
public static class It
{
    /// <summary>
    /// Generates a default value for type T.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The default value for the type T.</returns>
    public static T AnyValue<T>()
    {
        return It.IsAny<T>().Get();
    }

    /// <summary>
    /// Generates an instance of It<T> that matches any value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>An It<T> instance configured to match any value.</returns>
    public static It<T> IsAny<T>()
    {
        return new It<T>(x => true);
    }

    /// <summary>
    /// Generates a value of type T that matches the given condition.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="matcher">The condition function that the value needs to satisfy.</param>
    /// <returns>The value that matches the given condition.</returns>
    public static T IsAny<T>(Func<T, bool> matcher)
    {
        return new It<T>(matcher).Get();
    }
}

/// <summary>
/// Represents a conditional matcher for values of type T.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public class It<T>
{
    /// <summary>
    /// The function that defines the matching condition.
    /// </summary>
    private readonly Func<T, bool> _matcher;

    /// <summary>
    /// Initializes a new instance of the It class with a matching function.
    /// </summary>
    /// <param name="matcher">The function that defines the matching condition.</param>
    public It(Func<T, bool> matcher)
    {
        _matcher = matcher;
    }

    /// <summary>
    /// Checks whether a value matches the condition defined by the instance.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value matches, otherwise false.</returns>
    public bool Matches(T value)
    {
        return _matcher(value);
    }

    /// <summary>
    /// Retrieves the default value for type T.
    /// </summary>
    /// <returns>The default value for type T.</returns>
    public T Get()
    {
        return default;
    }
}
