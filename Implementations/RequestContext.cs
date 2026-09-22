namespace DiDemo;

public class RequestContext : IRequestContext
{
    public Guid RequestId { get; } = Guid.NewGuid();
}
