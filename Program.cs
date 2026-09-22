using DiDemo;
using Microsoft.Extensions.DependencyInjection;

Section("1. BEFORE - tightly coupled");
{
    var service = new BeforeOrderService();
    service.PlaceOrder(new Order { Id = 1, CustomerEmail = "alice@example.com", Total = 42.00m });
    Console.WriteLine("  -> BeforeOrderService can ONLY ever use SmtpEmailSender. No swapping, no faking it in a test.");
}

Section("2. AFTER - constructor injection, wired up by hand (no container yet)");
{
    IOrderRepository repository = new SqlOrderRepository("Server=prod-db;...");
    IEmailSender emailSender = new SmtpEmailSender("smtp.company.com");
    var service = new OrderService(repository, emailSender);

    service.PlaceOrder(new Order { Id = 2, CustomerEmail = "bob@example.com", Total = 17.50m });
    Console.WriteLine("  -> Same OrderService class could just as easily receive a PostgresOrderRepository. It doesn't know or care.");
}

Section("3. AFTER - using the real ASP.NET Core DI container");
{
    var services = new ServiceCollection();
    services.AddScoped<IOrderRepository, SqlOrderRepository>(_ => new SqlOrderRepository("Server=prod-db;..."));
    services.AddScoped<IEmailSender, SmtpEmailSender>(_ => new SmtpEmailSender("smtp.company.com"));
    services.AddScoped<OrderService>();

    using var provider = services.BuildServiceProvider();

    var service = provider.GetRequiredService<OrderService>();
    service.PlaceOrder(new Order { Id = 3, CustomerEmail = "carol@example.com", Total = 99.99m });
    Console.WriteLine("  -> We never wrote 'new OrderService(...)' - the container built it and injected both dependencies.");
}

Section("4. Why this matters: testing PlaceOrder with zero real infrastructure");
{
    var fakeRepo = new FakeOrderRepository();
    var fakeEmail = new FakeEmailSender();
    var service = new OrderService(fakeRepo, fakeEmail);

    var order = new Order { Id = 4, CustomerEmail = "dave@example.com", Total = 5.00m };
    service.PlaceOrder(order);

    bool orderWasSaved = fakeRepo.SavedOrders.Contains(order);
    bool emailWasSent = fakeEmail.SentEmails.Any(e => e.To == "dave@example.com");

    Console.WriteLine($"  Order saved to fake repository:  {orderWasSaved}");
    Console.WriteLine($"  Email sent via fake sender:       {emailWasSent}");
    Console.WriteLine("  -> No database, no SMTP server, no network - just OrderService's own logic, verified.");
}

Section("5. Lifetimes: Scoped vs Singleton vs Transient, side by side");
{
    var services = new ServiceCollection();
    services.AddScoped<IRequestContext, RequestContext>();

    using var provider = services.BuildServiceProvider();

    Console.WriteLine("  Two resolutions from the SAME scope (should be the SAME id for a scoped service):");
    using (var scope = provider.CreateScope())
    {
        var a = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        var b = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        Console.WriteLine($"    a.RequestId == b.RequestId : {a.RequestId == b.RequestId}  ({a.RequestId})");
    }

    Console.WriteLine("  Two DIFFERENT scopes (simulating two separate web requests):");
    using (var scope1 = provider.CreateScope())
    using (var scope2 = provider.CreateScope())
    {
        var reqInScope1 = scope1.ServiceProvider.GetRequiredService<IRequestContext>();
        var reqInScope2 = scope2.ServiceProvider.GetRequiredService<IRequestContext>();
        Console.WriteLine($"    scope1 id: {reqInScope1.RequestId}");
        Console.WriteLine($"    scope2 id: {reqInScope2.RequestId}");
        Console.WriteLine($"    Different ids, as expected for Scoped: {reqInScope1.RequestId != reqInScope2.RequestId}");
    }
}

Section("6. The classic bug: injecting a Scoped service into a Singleton");
{
    var services = new ServiceCollection();
    services.AddScoped<IRequestContext, RequestContext>();
    services.AddSingleton<BadSingletonService>();
    services.AddSingleton<GoodSingletonService>();

    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    Console.WriteLine("  Trying to resolve BadSingletonService (captures IRequestContext directly)...");
    try
    {
        var bad = provider.GetRequiredService<BadSingletonService>();
        Console.WriteLine($"    Unexpectedly succeeded, captured id: {bad.GetCapturedRequestId()}");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"    Container REFUSED to build it: {ex.Message}");
    }

    Console.WriteLine("  GoodSingletonService (asks for a fresh scope each time it needs the scoped service):");
    var good = provider.GetRequiredService<GoodSingletonService>();
    var id1 = good.GetFreshRequestId();
    var id2 = good.GetFreshRequestId();
    Console.WriteLine($"    call 1: {id1}");
    Console.WriteLine($"    call 2: {id2}");
    Console.WriteLine($"    Different each time, no stale data leaking across requests: {id1 != id2}");
}

Console.WriteLine();
Console.WriteLine("Done. Every section above ran with no database, no SMTP server, no external services -");
Console.WriteLine("that's the whole point: DI is what makes that possible.");

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
