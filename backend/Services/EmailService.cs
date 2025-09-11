using Azure;
using Azure.Communication.Email;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class EmailService
    {
        private readonly EmailClient _emailClient;

        public EmailService()
        {
            // Retrieve the connection string from the environment variable
            string connectionString = Environment.GetEnvironmentVariable("COMMUNICATION_SERVICES_CONNECTION_STRING")
                ?? throw new InvalidOperationException("COMMUNICATION_SERVICES_CONNECTION_STRING is not set.");
            _emailClient = new EmailClient(connectionString);
        }

        private static string FormatUkDateTime(DateTime utc)
        {
            try
            {
#if WINDOWS
                var tz = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
#else
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
#endif
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
                return local.ToString("dd.MM.yyyy HH:mm");
            }
            catch
            {
                return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm 'UTC'");
            }
        }

        public async Task SendPackageEmailAsync(
            string toEmail,
            string subject,
            int itemCount,
            string shippingProvider,
            string additionalInfo,
            DateTime collectionDateUtc,
            string? imageUrl = null,
            string? qrLink = null,
            string? qrImageDataUri = null) // ensure params exist
        {
            try
            {
                var when = FormatUkDateTime(collectionDateUtc);

                var qrHtml = string.IsNullOrWhiteSpace(qrLink) ? "" :
$@"<p><strong>Reception Link (QR):</strong><br/>
<a href=""{qrLink}"">{qrLink}</a><br/>
{(string.IsNullOrWhiteSpace(qrImageDataUri) ? "" : $@"<img src=""{qrImageDataUri}"" alt=""QR"" style=""width:200px;height:200px;margin-top:6px;"" />")}
</p>";

                var imageHtml = string.IsNullOrWhiteSpace(imageUrl) ? "" :
$@"<p><strong>Label Image:</strong><br/>
<a href=""{imageUrl}"" target=""_blank"">{imageUrl}</a><br/>
<img src=""{imageUrl}"" alt=""Label Image"" style=""max-width:480px;height:auto;border:1px solid #ddd;border-radius:4px;margin-top:6px;"" />
</p>";

                var emailBody = $@"
<html><body>
  <h1>{subject}</h1>
  <p><strong>Lecturer Email:</strong> {toEmail}</p>
  <p><strong>Item Count:</strong> {itemCount}</p>
  <p><strong>Shipping Provider:</strong> {shippingProvider}</p>
  <p><strong>Additional Information:</strong> {additionalInfo}</p>
  <p><strong>Collection Date/Time:</strong> {FormatUkDateTime(collectionDateUtc)}</p>
  {imageHtml}
  {qrHtml}
</body></html>";

                var emailContent = new EmailContent(subject)
                {
                    PlainText =
$@"Lecturer Email: {toEmail}
Item Count: {itemCount}
Shipping Provider: {shippingProvider}
Additional Information: {additionalInfo}
Collection Date/Time: {FormatUkDateTime(collectionDateUtc)}
{(string.IsNullOrWhiteSpace(imageUrl) ? "" : $"Label Image: {imageUrl}")}
{(string.IsNullOrWhiteSpace(qrLink) ? "" : $"Reception Link: {qrLink}")}",
                    Html = emailBody
                };

                var emailMessage = new EmailMessage(
                    senderAddress: "DoNotReply@c82bcbff-b02e-4e6f-af44-059a9fd518f9.azurecomm.net",
                    content: emailContent,
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(toEmail) })
                );

                // Send the email
                EmailSendOperation emailSendOperation = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage);
                Console.WriteLine($"Email sent successfully. Message ID: {emailSendOperation.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                throw;
            }
        }
    }
}