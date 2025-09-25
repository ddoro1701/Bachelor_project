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

        // Update signature to match controller call
        public async Task SendPackageEmailAsync(
            string toEmail,
            string subject,
            int itemCount,
            string shippingProvider,
            string additionalInfo,
            DateTime collectionDateUtc,
            string receptionUrl,
            string qrDataUri,
            string? imageUrl
        )
        {
            try
            {
                // Build HTML with extra info (QR and reception link)
                var collectionLocal = collectionDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var imgHtml = string.IsNullOrWhiteSpace(imageUrl) ? "" : $"<p><img alt=\"Package Image\" src=\"{imageUrl}\" style=\"max-width:480px;border:1px solid #ccc\"/></p>";
                var qrHtml = string.IsNullOrWhiteSpace(qrDataUri) ? "" : $"<p><img alt=\"QR Code\" src=\"{qrDataUri}\" style=\"width:180px;height:180px\"/></p>";

                var emailBody = $@"
<html>
  <body style=""font-family:Arial, Helvetica, sans-serif"">
    <h2>{subject}</h2>
    <p><strong>Lecturer Email:</strong> {toEmail}</p>
    <p><strong>Item Count:</strong> {itemCount}</p>
    <p><strong>Shipping Provider:</strong> {shippingProvider}</p>
    <p><strong>Additional Information:</strong> {System.Net.WebUtility.HtmlEncode(additionalInfo ?? string.Empty)}</p>
    <p><strong>Logged at:</strong> {collectionLocal}</p>
    {imgHtml}
    <p><a href=""{receptionUrl}"">Open collection page</a></p>
    {qrHtml}
    <hr/>
    <p><strong>Opening Hours</strong></p>
    <p>10am - 12pm</p>
    <p>2pm - 3pm</p>
    <p>Closed for Lunch: 12pm - 2pm</p>
    <p>Outgoing mail <strong>NO</strong> later than 2.30pm</p>
    <p>Thank you for your cooperation.</p>
    <p>Best regards,</p>
  </body>
</html>";

                var emailContent = new EmailContent(subject)
                {
                    PlainText = $"Lecturer Email: {toEmail}\nItem Count: {itemCount}\nShipping Provider: {shippingProvider}\nAdditional Information: {additionalInfo}\nLogged at (local): {collectionLocal}\nCollection page: {receptionUrl}",
                    Html = emailBody
                };

                var emailMessage = new EmailMessage(
                    senderAddress: "DoNotReply@c82bcbff-b02e-4e6f-af44-059a9fd518f9.azurecomm.net",
                    content: emailContent,
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(toEmail) })
                );

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