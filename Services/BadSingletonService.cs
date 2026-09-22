namespace DiDemo;

// If the container let this through, the singleton would capture one request's IRequestContext
// forever and reuse it for every later request. With ValidateScopes enabled, the container
// refuses to build it and throws instead.
public class BadSingletonService
{
    private readonly IRequestContext _requestContext;

    public BadSingletonService(IRequestContext requestContext) => _requestContext = requestContext;

    public Guid GetCapturedRequestId() => _requestContext.RequestId;
}
