namespace DiDemo;

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject)> SentEmails { get; } = new();

    public void Send(string to, string subject) => SentEmails.Add((to, subject));
}
