using Microsoft.Extensions.DependencyInjection;

namespace DiDemo;

public class GoodSingletonService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GoodSingletonService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public Guid GetFreshRequestId()
    {
        using var scope = _scopeFactory.CreateScope();
        var requestContext = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        return requestContext.RequestId;
    }
}
