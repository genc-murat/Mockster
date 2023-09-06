# Mockster

Mockster is a mocking framework for .NET, designed to make unit testing easier and more robust. It allows you to create mock objects for your tests, set up behaviors, and verify the interactions with those objects.

## Features

- **Create Mocks**: Easily create mock objects of any interface or abstract class.
- **Method Setup**: Define behavior for specific methods in the mock object.
- **Property Setup**: Assign fixed values to properties of the mock object.
- **Sequence Setup**: Define a sequence of results for a method.
- **Invocation History**: Retrieve a history of method calls made on the mock object.
- **Type Matching**: Use `It.IsAny<T>` for more flexible argument matching.
- **Exception Handling**: Automatically throws if a method that hasn't been set up is invoked.

## Getting Started

### Create Mocks

Create a mock object for an interface or abstract class.

```csharp
var mockUserService = Mockster.CreateMock<IUserService>();
```

### Set Up Method Behavior

You can specify what should happen when a method is invoked.

```csharp
var interceptor = Mockster.Config(mockUserService);

interceptor.SetupMethod(x => x.GetUserId("Alice"), "UserID123");
```

### Set Up Property Behavior

You can specify a property's return value.

```csharp
interceptor.SetupProperty(x => x.IsActive, true);
```

### Set Up Method Sequence

You can specify a sequence of return values for a method.

```csharp
interceptor.SetupSequence(x => x.RandomNumber(), new[] { 1, 2, 3 });
```

### Verify Calls

Check the number of times a method was invoked.

```csharp
interceptor.VerifyMethodCallCount(x => x.GetUserId("Alice"), 1);
```

### Retrieve Invocation History

You can get a history of method calls made on the mock object.

```csharp
var history = interceptor.GetInvocationHistory();
```

## Type Matching with `It`

`It` class provides the `IsAny<T>` and `AnyValue<T>` methods to allow more flexible argument matching.

```csharp
// Matches any value of type T
interceptor.SetupMethod(x => x.Method(It.AnyValue<int>()), "OK");

// Create a custom matcher
var itIsEven = It.IsAny<int>(x => x % 2 == 0);
```

## Configuration

To configure a mock object, you'll need to get its interceptor.

```csharp
var interceptor = Mockster.Config(mockUserService);
```