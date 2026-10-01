namespace DiDemo;

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
