using MimeKit;
using System.Net.Mail;

namespace FridgeManagement.Service
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailWithTemplateAsync(string toEmail, string subject, string templateName, Dictionary<string, string> placeholders)
        {
            // Load HTML template
            var template = await LoadEmailTemplateAsync(templateName);

            // Replace placeholders with actual values
            foreach (var placeholder in placeholders)
            {
                template = template.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
            }

            // Send the email with the filled template
            await SendEmailAsync(toEmail, subject, template);
        }

        private async Task<string> LoadEmailTemplateAsync(string templateName)
        {
            // Assuming templates are stored in the "Templates" folder
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateName);
            return await File.ReadAllTextAsync(templatePath);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("SMTP");

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(emailSettings["SenderName"], emailSettings["SenderEmail"]));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = body };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                await client.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]), false);
                await client.AuthenticateAsync(emailSettings["Username"], emailSettings["Password"]);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }
        }



    }
}
