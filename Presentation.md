# Inversion of Control & Dependency Injection — DiDemo (F4H-11258)

> **Note:** DiDemo is a plain .NET 8 **console** app. It only uses the `Microsoft.Extensions.DependencyInjection` package. There is no ASP.NET Core, no web host, no HTTP requests. Everything I say about ASP.NET Core in section 2 is how a real web app does it, not something this project runs.
> **Note:** `SqlOrderRepository` and `SmtpEmailSender` are stand-ins. They don't touch a database or send email. They just print a line to the console. When I say the "before" version would hit a real database and send a real email every time, that's what would happen with real classes. The demo doesn't show it.
> **Note:** There is no unit test project. Section 4 of `Program.cs` is a normal console section that checks two `bool`s and prints them. The walkthrough doc shows `Assert.Single(...)` and different variable names (`repository`, `emailSender`). That code is not in the project. The real code is shown in section 4 below.
> **Note:** `Program.cs` Section 5 is titled "Scoped vs Singleton vs Transient, side by side", but it only registers and tests **Scoped**. `AddTransient` is not used anywhere in the code. `AddSingleton` only shows up in Section 6. What I say about Transient and Singleton comes from how the container works, not from this demo's output.
> **Note:** `ValidateScopes` does not make the bug fail at startup. In the code it fails when `GetRequiredService<BadSingletonService>()` is called, not when the provider is built. (Failing at build time needs `ValidateOnBuild`, which the code doesn't use.) Also, the code never runs the bad singleton *without* validation, so the "first request's id leaks to everyone" story is explained, not shown.
> **Note:** In a real ASP.NET Core app, `ValidateScopes` is on by default in Development. I can't confirm that from this code, since there's no ASP.NET Core here.
> **Note:** Section 3's heading says "the real ASP.NET Core DI container". It's the same container ASP.NET Core uses, but here it's used on its own. Also, Section 3 resolves `OrderService` straight from the root provider, without creating a scope. That works because scope checks are off there, but it means those "Scoped" services really live as long as the provider. It doesn't show per-request behavior. Section 5 is the part that does.

---

## 1. IoC and DI, in my own words

### Inversion of Control

Normally, my class is the boss. It decides what it needs, and it creates it with `new`. It also decides when things happen. My code calls the libraries.

Inversion of Control flips that. My class stops creating its own helpers and stops running the show. Something outside it does that: a framework, or a DI container. My class just says "here's what I need" and waits. The outside code builds things, wires them together, and calls my code when it's time.

That's the Hollywood principle: **"Don't call us, we'll call you."** My class doesn't go out and get its email sender. It gets handed one. In ASP.NET Core it goes even further: I don't call my controller. The framework creates it, gives it what it needs, and calls it when a request comes in.

### Dependency Injection

DI is one specific way to do IoC, just for dependencies. A "dependency" is anything my class needs to do its job, like a repository or an email sender. With DI, my class doesn't create those things. They get passed in, usually through the constructor.

**What problem it solves:** if a class creates its own helpers, it's stuck with them forever. I can't swap them. I can't test the class without the real thing. If the setup changes (a new SMTP host, say), I have to edit a class that has nothing to do with email setup.

**How it ties to interfaces:** DI works best together with interfaces. If I inject a concrete `SmtpEmailSender`, I've moved where it's created, but I'm still stuck with that one class. If I inject an `IEmailSender`, my class only knows "something that can send an email". Now anyone can pass in the real sender, a fake one, or a new one we write next year, and my class doesn't change at all.

One thing I want to say clearly: **DI does not need a container.** Building the objects yourself and passing them in is already DI. DiDemo shows that in Section 2. A container just does the wiring for you when there are a lot of classes.

### Service Locator (and why DI is usually better)

Service Locator is the other way to get dependencies. Instead of getting things passed in, the class reaches out to some central registry and asks for them: `provider.GetRequiredService<IEmailSender>()`, right inside the class. It looks fine, but the dependencies are now hidden. You can't tell from the constructor what the class needs. You only find out when it fails at runtime. Tests are harder too, because you have to set up the whole locator, not just pass in two fakes. And every class now depends on the container itself.

With constructor injection, the constructor *is* the list of what the class needs. It's honest, and the compiler checks it. In DiDemo, the only places that call `GetRequiredService` are `Program.cs` (the top of the app, where wiring belongs) and `GoodSingletonService`. That second one is the known, accepted exception: a singleton that needs a fresh scoped object each time has to ask for it. That's the case where a small dose of "locator" is the right tool. It's not the default.

---

## 2. How DI is set up in a typical ASP.NET Core app

First, to be clear: **DiDemo is a console app, not a web app.** But it uses the exact same DI library that ASP.NET Core uses under the hood. So the registration calls are the same. Only the setup around them is different.

### Two steps: register, then resolve

**Register** = tell the container "when someone asks for X, give them Y, and keep it alive this long."
**Resolve** = ask the container for something, and it builds it (and everything it needs).

Here's what DiDemo does, in `Program.cs` Section 3:

```csharp
var services = new ServiceCollection();
services.AddScoped<IOrderRepository, SqlOrderRepository>(_ => new SqlOrderRepository("Server=prod-db;..."));
services.AddScoped<IEmailSender, SmtpEmailSender>(_ => new SmtpEmailSender("smtp.company.com"));
services.AddScoped<OrderService>();

using var provider = services.BuildServiceProvider();

var service = provider.GetRequiredService<OrderService>();
service.PlaceOrder(new Order { Id = 3, CustomerEmail = "carol@example.com", Total = 99.99m });
```

- `new ServiceCollection()` is the list of registrations.
- The first two lines use a small factory (`_ => new ...`). Why? Because `SqlOrderRepository` and `SmtpEmailSender` need a string (connection string, host name) in their constructor. The container can't guess a string, so I tell it how to build them.
- `AddScoped<OrderService>()` has no factory. `OrderService` only needs `IOrderRepository` and `IEmailSender`, and those are already registered. So the container works it out by itself.
- `BuildServiceProvider()` turns the list into the actual container.
- `GetRequiredService<OrderService>()` is the resolve step. I never wrote `new OrderService(...)`. The container did it, and passed in both dependencies.

### The same thing in a real ASP.NET Core app

In a web app, this goes in `Program.cs`, and `builder.Services` *is* the `ServiceCollection`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>(_ => new SqlOrderRepository("Server=prod-db;..."));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>(_ => new SmtpEmailSender("smtp.company.com"));
builder.Services.AddScoped<OrderService>();

var app = builder.Build();
```

(This is an illustration, not code from DiDemo. In a real app the connection string would come from config, not be typed in.)

The big difference is resolving. In a web app **I usually never call `GetRequiredService` myself.** I just put `OrderService` in a controller's constructor, and the framework creates the controller for each request and fills it in. That's the "we'll call you" part.

### What "Scoped" really means

- **In ASP.NET Core:** one scope = **one HTTP request**. The framework opens a scope when a request comes in and throws it away when the response is sent. Everything Scoped is shared inside that one request, and fresh for the next one.
- **In DiDemo:** there are no requests, so the demo fakes them by creating scopes by hand with `provider.CreateScope()`. Each `IServiceScope` stands in for one request.

DiDemo proves this in Section 5 with `RequestContext`, which just makes a new `Guid` when it's created:

```csharp
using (var scope = provider.CreateScope())
{
    var a = scope.ServiceProvider.GetRequiredService<IRequestContext>();
    var b = scope.ServiceProvider.GetRequiredService<IRequestContext>();
    Console.WriteLine($"    a.RequestId == b.RequestId : {a.RequestId == b.RequestId}  ({a.RequestId})");
}
```

Same scope → same object → same id (`True`). Then two different scopes → two different ids. That's exactly what "one per request" looks like.

### The three lifetimes

| Lifetime | What you get | Use it for | DiDemo shows it? |
| --- | --- | --- | --- |
| **Transient** (`AddTransient`) | A brand new object every time anyone asks | Small, cheap things with no state worth sharing | No, not used anywhere |
| **Scoped** (`AddScoped`) | One object per scope (per request in a web app) | Things tied to one request: DB context, current user, unit of work | Yes, Section 5 |
| **Singleton** (`AddSingleton`) | One object for the whole app, forever | Shared, thread-safe things: config, caches, clients meant to be reused | Yes, Section 6 |

**What goes wrong when you pick the wrong one:**

- **Singleton for something that holds per-request data:** one user's data leaks into another user's request. Also, many requests hit the same object at once, so it has to be thread-safe.
- **Transient for something expensive:** you keep building heavy objects over and over for no reason.
- **Scoped or Transient for something meant to be shared** (like a cache): every request gets its own, so nothing is really shared.
- **The big one — a Singleton that takes a Scoped thing in its constructor.** This is the "captive dependency". DiDemo shows it in Section 6 with `BadSingletonService`:

```csharp
public class BadSingletonService
{
    private readonly IRequestContext _requestContext;

    public BadSingletonService(IRequestContext requestContext) => _requestContext = requestContext;

    public Guid GetCapturedRequestId() => _requestContext.RequestId;
}
```

The singleton is built once, so it grabs one `IRequestContext` and keeps it forever. Every later "request" would see the first request's id. In the demo, the provider is built with `ValidateScopes = true`, so the container refuses. When I run it, it prints:

`Cannot consume scoped service 'DiDemo.IRequestContext' from singleton 'DiDemo.BadSingletonService'.`

The fix is `GoodSingletonService`. It takes `IServiceScopeFactory` (which is safe to keep forever) and makes a short scope each time it needs the scoped thing:

```csharp
public Guid GetFreshRequestId()
{
    using var scope = _scopeFactory.CreateScope();
    var requestContext = scope.ServiceProvider.GetRequiredService<IRequestContext>();
    return requestContext.RequestId;
}
```

Two calls → two different ids. No stale data.

Simple rule: **an object should only depend on things that live at least as long as it does.**

---

## 3. How it works (matches the drawing)

> **Note:** Under the row 1 interface, the caption says "the container decides which one you get." Section 1 says DI doesn't need a container: whoever builds the object decides. In DiDemo the fake is passed in by hand (`new OrderService(fakeRepo, fakeEmail)`), not picked by a container.
> **Note:** The row 2 code lines say `services.AddScoped<IEmailSender, SmtpEmailSender>();` with no factory. Section 2 explains why DiDemo *needs* a factory there (`SmtpEmailSender` takes a host-name string the container can't guess). Also, the `// in tests` line registering `FakeEmailSender` in the container isn't how DiDemo tests. It passes fakes by hand. Read both lines as "the idea", not real code.
> **Note:** The two dashed "implements" arrows point *from* `IEmailSender` *to* the two classes. The usual way to draw it is the other way: the class points at the interface it implements. Only one "implements" label covers both arrows.
> **Note:** The "Captive dependency!" box says the singleton "leaks the first request's state forever." Section 2 says that's only what *would* happen. With `ValidateScopes` on, the container refuses to build it. The drawing doesn't mention `ValidateScopes`.
> **Note:** Row 1 only shows `BeforeOrderService` creating `SmtpEmailSender`. The real class now also creates a `SqlOrderRepository`. The drawing also has nothing about IoC / the Hollywood principle, building objects by hand (DI without a container), or Service Locator. Those parts of section 1 aren't in the drawing.

The drawing is called **"Dependency Injection — Theory Map"**, with the subtitle *"DiDemo: from tight coupling to a testable, lifetime-safe container."* It has three numbered rows, top to bottom. Here's what I say while pointing at each one.

### Row 1 — "Tight coupling → depend on an abstraction"

**Left side (red = the problem):**

1. **`BeforeOrderService`** (red box) — The "before" class.
2. **`SmtpEmailSender`** (red box) — The email class it uses.
3. **Arrow `BeforeOrderService` → `SmtpEmailSender`**, labeled **"new (hard-coded)"** — "The service makes its own email sender with `new`. It's welded to that one class."
4. Caption under it: **"Nothing can be swapped out or tested in isolation."**

**Right side (green and blue = the fix):**

5. **`OrderService`** (green box) — The "after" class.
6. **`IEmailSender`** (blue box) — The interface. Blue in this drawing means "abstraction / container stuff".
7. **Arrow `OrderService` → `IEmailSender`**, labeled **"constructor"** — "Now it doesn't make anything. It just asks for an `IEmailSender` in its constructor."
8. **`SmtpEmailSender (real)`** (green box, top) and **`FakeEmailSender (test)`** (purple box, bottom) — The two classes that can sit behind the interface.
9. **Two dashed arrows from `IEmailSender`** to those two boxes, with one label: **"implements"** — "Either one fits in that slot."
10. Caption: **"Two implementations, one interface — the container decides which one you get."**

What I say: *"Left is the problem: the class builds its own helper. Right is the fix: the class only knows the interface, and someone outside picks real or fake."*

### Row 2 — "The container wires the graph"

Three boxes in a line, left to right:

1. **`ServiceCollection`** (blue box) — The list of registrations.
2. **Arrow →**, labeled **`BuildServiceProvider()`**
3. **`ServiceProvider`** (blue box) — The actual container, built from the list.
4. **Arrow →**, labeled **`GetRequiredService<T>()`**
5. **`OrderService`** (green box) — What comes out, already filled in.

Under the row, two lines of code:

- `services.AddScoped<IEmailSender, SmtpEmailSender>();`
- `services.AddScoped<IEmailSender, FakeEmailSender>();   // in tests`

What I say: *"Register into the collection, build the provider, ask for what you need. I never call `new OrderService(...)`. Same registration, different implementation, and you've swapped real for fake."*

### Row 3 — "Lifetimes decide who shares an instance"

Three grey boxes, then one red box:

1. **Transient** — "New instance every resolve"
2. **Scoped** — "One instance per scope (request)"
3. **Singleton** — "One instance for the whole app"
4. **Captive dependency!** (red box, right) — "A Singleton holding a Scoped instance leaks the first request's state forever."
5. **Short red arrow from the Singleton box → the Scoped box** (across the gap between them), labeled **"Singleton → Scoped"** just above it — "This is the bug: a long-lived thing holding a short-lived thing. That's what the red box on the right is warning about."

Under the row, the last line of the drawing:

**"Fix: inject IServiceScopeFactory into the Singleton and create a scope on demand."**

What I say: *"Three lifetimes, from shortest to longest. The rule is: never let a long one hold a short one. If a singleton needs scoped stuff, it gets the scope factory and makes a fresh scope each time."*

**How they connect (all arrows in the drawing):**
- `BeforeOrderService` → `SmtpEmailSender`: "new (hard-coded)" (solid, red)
- `OrderService` → `IEmailSender`: "constructor" (solid, green)
- `IEmailSender` → `SmtpEmailSender (real)`: "implements" (dashed, blue)
- `IEmailSender` → `FakeEmailSender (test)`: "implements" (dashed, blue, shares the one label)
- `ServiceCollection` → `ServiceProvider`: "BuildServiceProvider()" (solid, blue)
- `ServiceProvider` → `OrderService`: "GetRequiredService<T>()" (solid, green)
- Singleton → Scoped: "Singleton → Scoped" (solid, red)

Transient isn't connected to anything, and nothing points at the "Captive dependency!" box. It's a warning box at the right end of the row, explaining what the Singleton → Scoped arrow means. The rows aren't connected to each other by arrows.

---

## 4. A concrete C# example — before and after DI

### Before: `Services/BeforeOrderService.cs`

```csharp
public class BeforeOrderService
{
    private readonly SqlOrderRepository _repository;
    private readonly SmtpEmailSender _emailSender;

    public BeforeOrderService()
    {
        _repository = new SqlOrderRepository("Server=prod-db;...");
        _emailSender = new SmtpEmailSender("smtp.company.com");
    }

    public void PlaceOrder(Order order)
    {
        _repository.Save(order);
        _emailSender.Send(order.CustomerEmail, "Order confirmed");
    }
}
```

### After: `Services/OrderService.cs`

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

Both classes do the exact same two things: save the order, then send the email. `PlaceOrder` is the same, line for line. The only difference is **how each class gets its helpers.**

### What changed, line by line

1. **The field types.** `SqlOrderRepository _repository` → `IOrderRepository _repository`, and `SmtpEmailSender _emailSender` → `IEmailSender _emailSender`. Before, the class was tied to two exact classes. Now it only knows "something that can save an order" and "something that can send email". It never learns it's SQL or SMTP.
2. **The constructor.** Before: no parameters, and it built both helpers itself with `new`. After: it *asks* for both as parameters. This is the actual injection. Control over *which* repository and sender to use moved out of the class, to whoever calls the constructor.
3. **The connection string and host name are gone.** `"Server=prod-db;..."` and `"smtp.company.com"` were hard-coded inside an order class. Now those details live where the app is wired up (`Program.cs`). The order class doesn't need to know them.
4. **Null checks.** `?? throw new ArgumentNullException(...)`. If someone forgets to pass a dependency, it fails right away, at creation, with a clear message, not later in the middle of `PlaceOrder`.
5. **No `new` anywhere inside the class.** This is the easiest thing to check. If a service has `new SomeHelper(...)` inside it, it's probably still tightly coupled.
6. **`PlaceOrder` didn't change at all.** Same two calls, same order. The business logic stays the same. Only *where its helpers come from* changed. That's the whole point: same behavior, better construction.

### What we actually gained

- **Testing without real infrastructure.** I can pass in fakes. No database, no mail server, no network. Tests run in milliseconds. (Proof below.)
- **Swapping implementations without touching `OrderService`.** A different database, a different email provider, a "log instead of send" sender for local dev: each is a new class that implements the interface. Wiring changes in one place, and `OrderService` stays exactly as it is. Section 2 and Section 3 of `Program.cs` use the *same* `OrderService` class with two different wiring styles.
- **Setup lives in one place.** Hosts and connection strings are in `Program.cs`, not spread across business classes.
- **Honest constructor.** Just by reading the constructor I know exactly what `OrderService` needs.
- **The container can build it.** Because the needs are in the constructor, `AddScoped<OrderService>()` works with no extra code (Section 3).

### Proof: swapping in fakes (`Program.cs` Section 4)

The fakes are tiny. They just remember what was called:

```csharp
public class FakeOrderRepository : IOrderRepository
{
    public List<Order> SavedOrders { get; } = new();

    public void Save(Order order) => SavedOrders.Add(order);
}

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject)> SentEmails { get; } = new();

    public void Send(string to, string subject) => SentEmails.Add((to, subject));
}
```

And here's the real check from `Program.cs`:

```csharp
var fakeRepo = new FakeOrderRepository();
var fakeEmail = new FakeEmailSender();
var service = new OrderService(fakeRepo, fakeEmail);

var order = new Order { Id = 4, CustomerEmail = "dave@example.com", Total = 5.00m };
service.PlaceOrder(order);

bool orderWasSaved = fakeRepo.SavedOrders.Contains(order);
bool emailWasSent = fakeEmail.SentEmails.Any(e => e.To == "dave@example.com");
```

Both print `True`. It's the same `OrderService` that runs with the "real" classes in Sections 2 and 3. Nothing in it changed. I only changed what I passed in.

With `BeforeOrderService`, this is impossible. There's no way to give it a fake. It always builds its own `SqlOrderRepository` and `SmtpEmailSender`. That's the whole difference in one picture.

(This is a console check, not a real unit test. In a test project, those two `bool`s would just become `Assert` calls.)

---

## 5. My own opinion

> ✍️ **To fill in myself — don't read this prompt out loud.**
> How important do I actually think DI is, and why? Think about:
> - Where it has really helped me (or would have) in our own code. One real example beats a general claim.
> - Is it always worth it? When would I skip it (tiny scripts, simple helpers)?
> - What do I find hard or annoying about it (too many constructor parameters, lifetime bugs, "where does this come from?" when debugging)?
> - My one-sentence bottom line.

_(my opinion here)_
