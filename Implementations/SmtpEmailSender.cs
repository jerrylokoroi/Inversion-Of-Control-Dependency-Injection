namespace DiDemo;

public class SmtpEmailSender : IEmailSender
{
    private readonly string _smtpHost;

    public SmtpEmailSender(string smtpHost) => _smtpHost = smtpHost;

    public void Send(string to, string subject) =>
        Console.WriteLine($"  [SmtpEmailSender via {_smtpHost}] emailing {to}: \"{subject}\"");
}
