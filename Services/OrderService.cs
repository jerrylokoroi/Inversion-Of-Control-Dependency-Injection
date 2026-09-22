namespace DiDemo;

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
