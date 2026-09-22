# DiDemo: Dependency Injection Walkthrough

As of September 22, 2026

This is the companion reference for the DiDemo presentation: what the project teaches, how the pieces fit together, and the exact code worth putting on screen.

## Overview

DiDemo is a small, self-contained .NET 8 console project (about 120 lines across 9 files) built to teach one thing well: how and why to use Dependency Injection, on top of Microsoft.Extensions.DependencyInjection.

The running example is placing an order: something has to save it, and something has to email a confirmation. Every act in the demo reuses this same example, so the audience only has to learn one domain.

`Program.cs` runs as six numbered sections, each printing its own output to the console:

1. Tight coupling (the anti-pattern)
2. Manual DI (constructor injection, wired by hand)
3. Container DI (ServiceCollection / ServiceProvider)
4. Testability (swap in fakes, assert on recorded state)
5. Service lifetimes (Scoped vs. Singleton, proven via scope identity)
6. The captive dependency bug (Scoped inside Singleton) and its fix

Target framework: net8.0. Single NuGet dependency: Microsoft.Extensions.DependencyInjection 8.0.0 — no ASP.NET Core, no hosting package, just the raw container.

## Architecture at a glance

| Abstraction | Real implementation | Fake (for tests) |
| --- | --- | --- |
| IOrderRepository | SqlOrderRepository | FakeOrderRepository |
| IEmailSender | SmtpEmailSender | FakeEmailSender |
| IRequestContext | RequestContext | — (used for lifetimes, not testability) |

`Order` (`Models/Order.cs`) is a plain POCO — `Id`, `CustomerEmail`, `Total` — with no behavior. Every service in the demo depends only on the left-hand column; it never knows which implementation it's actually holding.

## Code walkthrough: the six acts

### Act 1 — Tight coupling (the problem)

`Services/BeforeOrderService.cs`

```csharp
public class BeforeOrderService
{
    private readonly SmtpEmailSender _emailSender;

    public BeforeOrderService()
        => _emailSender = new SmtpEmailSender("smtp.company.com");

    public void PlaceOrder(Order order)
        => _emailSender.Send(order.CustomerEmail, "Order confirmed");
}
```

The constructor builds its own collaborator. Nothing can swap `SmtpEmailSender` out, and testing `PlaceOrder` means sending a real email.

### Act 2 — Constructor injection (the fix)

`Services/OrderService.cs`

```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IEmailSender _emailSender;

    public OrderService(IOrderRepository repository, IEmailSender emailSender)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public void PlaceOrder(Order order)
    {
        _repository.Save(order);
        _emailSender.Send(order.CustomerEmail, "Order confirmed");
    }
}
```

Both dependencies are interfaces, checked once at construction, and never `new`'d inside the class.

### Act 3 — Manual DI (no container)

`Program.cs`, Section 2

```csharp
var repository = new SqlOrderRepository("Server=prod-db;...");
var emailSender = new SmtpEmailSender("smtp.company.com");
var service = new OrderService(repository, emailSender);
service.PlaceOrder(order);
```

This is still dependency injection — the dependencies are built outside the class and passed in. A container is a convenience for wiring object graphs at scale, not a requirement.

### Act 4 — Container DI

`Program.cs`, Section 3

```csharp
var services = new ServiceCollection();
services.AddScoped<IOrderRepository, SqlOrderRepository>(
    _ => new SqlOrderRepository("Server=prod-db;..."));
services.AddScoped<IEmailSender, SmtpEmailSender>(
    _ => new SmtpEmailSender("smtp.company.com"));
services.AddScoped<OrderService>();

using var provider = services.BuildServiceProvider();
var service = provider.GetRequiredService<OrderService>();
```

The factory-delegate overload of `AddScoped` is used because `SqlOrderRepository` and `SmtpEmailSender` take primitive constructor arguments (connection strings) the container can't supply automatically.

### Act 5 — Testing with fakes

`Program.cs`, Section 4

```csharp
var repository = new FakeOrderRepository();
var emailSender = new FakeEmailSender();
var service = new OrderService(repository, emailSender);

service.PlaceOrder(order);

Assert.Single(repository.SavedOrders);
Assert.Single(emailSender.SentEmails);
```

No real database, no real SMTP server. Assertions run against recorded state (`SavedOrders`, `SentEmails`) rather than side effects.

### Act 6 — Lifetimes and the captive dependency bug

`Program.cs`, Sections 5–6, plus `Services/BadSingletonService.cs` and `Services/GoodSingletonService.cs`

Section 5 registers `IRequestContext` as Scoped and proves scope identity: two resolutions from the same `IServiceScope` return the same `RequestId`; two different scopes return different ids.

Section 6 shows the trap:

```csharp
public class BadSingletonService
{
    private readonly IRequestContext _context;

    public BadSingletonService(IRequestContext context)
        => _context = context;
}

services.AddSingleton<BadSingletonService>();
// new ServiceProviderOptions { ValidateScopes = true }
provider.GetRequiredService<BadSingletonService>();
// -> InvalidOperationException
```

A Singleton captures a Scoped `IRequestContext` directly. Without `ValidateScopes = true`, the first resolution's `RequestId` would silently leak to every future caller. With it, the container throws at resolution time instead.

The fix — inject the scope factory, not the instance:

```csharp
public class GoodSingletonService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GoodSingletonService(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public Guid GetFreshRequestId()
    {
        using var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IRequestContext>()
            .RequestId;
    }
}
```

A Singleton that needs Scoped data creates its own scope on demand, every time it needs one.

## Key takeaways

1. Depend on abstractions, not concrete types.
2. A container is optional — it's a convenience at scale, not a requirement for DI.
3. Constructor injection plus fakes gives fast, real unit tests with zero infrastructure.
4. Match lifetimes (Transient, Scoped, Singleton) to how long state should actually live.
5. Never let a Singleton capture a Scoped dependency directly — inject `IServiceScopeFactory` instead.
6. Turn on `ValidateScopes = true` so captive dependencies fail loudly at startup, not silently in production.

## Running it yourself

```
dotnet run
```

from the `DiDemo` project directory steps through all six sections in order, printing each one's console output as it goes — a natural live companion to the slide deck during the talk.
