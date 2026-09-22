# Speaker Script — Dependency Injection in .NET (DiDemo)

A full word-for-word talk track for the 13-slide deck (`DiDemo-Dependency-Injection.pptx`). Estimated total runtime: **15–18 minutes** at a comfortable pace, plus Q&A. Adjust the bracketed timings to your own pace.

Companion files: `DiDemo-Dependency-Injection.pptx` (slides), `DI-Walkthrough-Reference.md` (written reference + full code), `DiDemo-Theory-Diagram.excalidraw` (whiteboard-style theory map — open on a second screen or sketch live from it).

---

## Slide 1 — Cover (~30s)

"Today I want to walk you through Dependency Injection in .NET — not as an abstract pattern, but through one small, real project called DiDemo. Everything I show you is actual, runnable code. My goal isn't to convince you DI is a good idea in the abstract — it's to show you exactly what problem it solves, step by step, using one running example: placing an order."

*(Advance)*

## Slide 2 — Agenda (~45s)

"We'll go through six acts, each building on the last, all using the same example: an order that needs to be saved and needs to trigger a confirmation email.

We'll start with the naive version — tight coupling. Then we'll wire dependencies by hand, without any framework. Then we'll bring in the actual DI container. Once that's in place, we'll see the real payoff: testing without touching a database or an SMTP server. Then we'll talk about lifetimes — Transient, Scoped, Singleton — and finish with a real bug class: the captive dependency, and how to fix it."

*(Advance)*

## Slide 3 — Tight coupling (~90s)

"Here's `BeforeOrderService`. Look at the constructor: it creates its own `SmtpEmailSender`, hard-coded, right there — `new SmtpEmailSender("smtp.company.com")`.

This works. It compiles, it runs, it sends the email. So what's wrong with it?

*(pause, point to bullets)*

First — nothing can swap that email sender out. If I want to test `PlaceOrder`, I am sending a real email, every single time. Second, if the SMTP configuration changes, I'm editing this class, even though this class's job is placing orders, not configuring email infrastructure. And fundamentally — this class depends on a concrete type. It's tightly bound to one specific implementation, forever."

*(Advance)*

## Slide 4 — Constructor injection (~90s)

"Here's the fix, and it's smaller than people expect. `OrderService` takes two interfaces — `IOrderRepository` and `IEmailSender` — as constructor parameters. That's it. That's the whole pattern.

Notice what's different: both parameters are abstractions, not concrete classes. They're null-checked once, at construction, so we fail fast if something's missing. And critically — there is no `new` anywhere inside this class. `OrderService` does not know, and does not care, whether it's talking to a real SQL database and a real SMTP server, or something else entirely. That 'something else' is the whole point of the next few slides."

*(Advance)*

## Slide 5 — Manual DI (~60s)

"Now — how do we actually get `OrderService` an `IOrderRepository` and an `IEmailSender`? The simplest possible way: just construct them and pass them in.

```
var repository = new SqlOrderRepository(...);
var emailSender = new SmtpEmailSender(...);
var service = new OrderService(repository, emailSender);
```

I want to be really clear about something here: **this is already dependency injection.** There's no framework involved. The dependencies are built outside the class and handed to it. A DI container is a convenience for doing this at scale — when you have dozens or hundreds of services — not a requirement for the pattern itself."

*(Advance)*

## Slide 6 — The container (~90s)

"Once you have more than a handful of services, wiring everything by hand gets tedious and error-prone. That's what `Microsoft.Extensions.DependencyInjection` is for.

We register our types with a `ServiceCollection`, build a `ServiceProvider` from it, and ask the provider for what we need.

One detail worth calling out: we're using the factory-delegate overload of `AddScoped` here — `_ => new SqlOrderRepository(...)` — instead of the simpler `AddScoped<TInterface, TImplementation>()`. That's because `SqlOrderRepository` and `SmtpEmailSender` take a connection string or a host name in their constructor — a primitive the container has no way to guess. When your implementation only needs other registered services, you can drop the factory and let the container wire it up entirely on its own."

*(Advance)*

## Slide 7 — Architecture (~60s)

"Let's step back and look at the shape of this. Three abstractions in this project: `IOrderRepository`, `IEmailSender`, `IRequestContext`. Each one has a real implementation for production, and — for the first two — a fake for tests.

This table *is* the architecture. `OrderService`, and the two singleton services we'll see later, only ever look at the left-hand column. They are never allowed to know what's actually sitting behind that interface at runtime."

*(Advance)*

## Slide 8 — Testability payoff (~75s)

"And here's why we bother with all of this. In a unit test, I swap in `FakeOrderRepository` and `FakeEmailSender` — both are in-memory classes that just record what was called.

I call `PlaceOrder`. No real database touched. No real SMTP server touched. Then I assert against `repository.SavedOrders` and `emailSender.SentEmails` — recorded state, not side effects. This test runs in milliseconds and needs zero infrastructure. This is the entire economic argument for depending on abstractions."

*(Advance)*

## Slide 9 — Lifetimes (~90s)

"Now, a different axis entirely: not *what* you inject, but *how long the container keeps it alive*.

Three lifetimes: **Transient** — a brand-new instance every single time it's resolved. **Scoped** — one instance per scope, which in a web app usually means per HTTP request. **Singleton** — one instance for the entire lifetime of the application.

We prove this with `IRequestContext`, which just wraps a `Guid` generated once at construction. Resolve it twice from the *same* scope — same `RequestId`, both times. Resolve it from two *different* scopes — simulating two separate web requests — and you get two different ids. Hold onto that mental model, because it's exactly what the next slide breaks."

*(Advance)*

## Slide 10 — The captive dependency bug (~90s)

"Here's a bug class that is extremely easy to introduce by accident and genuinely nasty in production.

`BadSingletonService` takes an `IRequestContext` — our Scoped service — directly in its constructor. Looks completely normal. But we've registered `BadSingletonService` itself as a **Singleton**.

Think through what that means: the container builds `BadSingletonService` exactly once, and at that moment, it resolves *one* `IRequestContext` — with *one* `RequestId` — and stores it in a field. Every future caller, for the entire lifetime of the app, gets that same original request's id. That's the captive dependency: a long-lived object silently holding onto short-lived state.

The good news: if you set `ValidateScopes = true` when you build the provider — which you should always do in development — the container catches this at the moment you try to resolve it, and throws an `InvalidOperationException` instead of silently corrupting data."

*(Advance)*

## Slide 11 — The fix (~75s)

"The fix is a specific, memorable pattern: don't inject the Scoped service into the Singleton. Inject `IServiceScopeFactory` instead — which is itself a Singleton, so it's a legal thing to hold onto forever.

Then, whenever `GoodSingletonService` actually needs fresh, correctly-scoped data, it creates its own scope on the spot, resolves `IRequestContext` from *that* scope, reads what it needs, and lets the scope go. Every call gets genuinely fresh, correctly-scoped data — because it's creating its own miniature 'request' each time it asks."

*(Advance)*

## Slide 12 — Takeaways (~45s)

"Six rules, and I'd genuinely suggest writing these on a sticky note:

Depend on abstractions, not concrete types. A container is optional — a convenience at scale, not a requirement. Constructor injection plus fakes gets you fast, real unit tests. Match lifetimes to how long state should actually live. Never let a Singleton capture a Scoped dependency directly. And — turn on `ValidateScopes`. Fail loudly at startup, not silently in production."

*(Advance)*

## Slide 13 — Closing (~30s)

"That's the whole story, end to end, in about 120 lines of actual code. Everything I showed you is in the DiDemo repo — clone it, run `dotnet run`, and you'll see all six sections execute and print their own output, in order. Happy to take questions."

---

## Anticipated questions (prep notes, not slides)

**"Why not just use a mocking framework instead of hand-written fakes?"**
Fakes are simpler to reason about for a small number of collaborators and don't require a mocking library dependency; for larger surfaces, Moq/NSubstitute are perfectly reasonable — the underlying principle (depend on the interface) is identical either way.

**"Doesn't constructor injection with many dependencies get unwieldy?"**
Yes — a constructor with 6+ parameters is usually a signal the class is doing too much and should be split, not a reason to abandon DI.

**"What about `AddTransient` — why isn't it demoed?"**
It's mentioned but not demoed directly; it behaves exactly as described (new instance every resolve) and follows the same registration syntax as `AddScoped`/`AddSingleton`.

**"Is `ValidateScopes` on by default?"**
No — it's on by default in ASP.NET Core's built-in host in Development, but off in a raw `ServiceCollection.BuildServiceProvider()` unless you pass `new ServiceProviderOptions { ValidateScopes = true }` explicitly, which is what Section 6 does.
