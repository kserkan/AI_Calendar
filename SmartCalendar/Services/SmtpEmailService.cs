using System.Net.Mail;
using System.Net;

public class SmtpEmailService
{
    private readonly IConfiguration _config;

    public SmtpEmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var client = new SmtpClient(_config["SmtpSettings:Host"], int.Parse(_config["SmtpSettings:Port"]))
        {
            Credentials = new NetworkCredential(_config["SmtpSettings:User"], _config["SmtpSettings:Pass"]),
            EnableSsl = bool.Parse(_config["SmtpSettings:EnableSsl"])
        };

        var mail = new MailMessage
        {
            From = new MailAddress(_config["SmtpSettings:User"]),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mail.To.Add(to);
        await client.SendMailAsync(mail);
    }
}
