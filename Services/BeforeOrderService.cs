namespace DiDemo;

public class BeforeOrderService
{
    private readonly SmtpEmailSender _emailSender;

    public BeforeOrderService() => _emailSender = new SmtpEmailSender("smtp.company.com");

    public void PlaceOrder(Order order) => _emailSender.Send(order.CustomerEmail, "Order confirmed");
}
