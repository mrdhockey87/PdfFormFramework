using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using System.Net.Mail;
using System.IO;

namespace PdfFormFramework.Printing
{
    public partial class PdfPrinterHelper
    {
        public static Task PrintOrEmailAsync(string filePath) =>
            PlatformPrintOrEmailAsync(filePath);

        public static partial Task PlatformPrintOrEmailAsync(string filePath);

        // New: Prompt the user for Subject and Recipient, fixed body = "See attached PDF."
        // Returns true if the email compose UI was shown (or Share fallback).
        public static async Task<bool> PromptAndEmailAsync(
            string filePath,
            string? defaultSubject = null,
            string? defaultRecipient = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return false;

                var page = GetActivePage();

                // single subject variable used across all branches to avoid shadowing
                string emailSubject = defaultSubject ?? $"Document: {Path.GetFileName(filePath)}";
                string? recipientsRaw = null;

                if (page is null)
                {
                    // No page to host prompts; fallback to direct email with defaults.
                    return await EmailOnlyAsync(filePath, subject: emailSubject, body: "See attached PDF.");
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var subjectInput = await page.DisplayPromptAsync(
                        "Email PDF",
                        "Enter a subject:",
                        accept: "Next",
                        cancel: "Cancel",
                        placeholder: "Subject",
                        maxLength: 200,
                        keyboard: Keyboard.Text,
                        initialValue: emailSubject);

                    if (string.IsNullOrWhiteSpace(subjectInput))
                        return; // cancelled

                    emailSubject = subjectInput;

                    recipientsRaw = await page.DisplayPromptAsync(
                        "Email PDF",
                        "Enter recipient email (you can separate multiple with , or ;):",
                        accept: "Send",
                        cancel: "Cancel",
                        placeholder: "name@example.com",
                        maxLength: 512,
                        keyboard: Keyboard.Email,
                        initialValue: defaultRecipient ?? "");
                });

                if (string.IsNullOrWhiteSpace(recipientsRaw))
                    return false; // user cancelled

                var recipients = recipientsRaw
                    .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                // Basic validation
                if (recipients.Count == 0 || recipients.Any(r => !IsValidEmail(r)))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        page.DisplayAlert("Invalid email", "Please enter a valid email address.", "OK"));
                    return false;
                }

                return await EmailOnlyAsync(
                    filePath,
                    subject: emailSubject,
                    body: "See attached PDF.",
                    to: recipients);
            }
            catch
            {
                return false;
            }
        }

        // Existing: Email-only flow (cross-platform)
        // Returns true if the compose UI was shown (or Share sheet as fallback).
        public static async Task<bool> EmailOnlyAsync(
            string filePath,
            string? subject = "Completed Form",
            string? body = "Please find the completed form attached.",
            IEnumerable<string>? to = null,
            IEnumerable<string>? cc = null,
            IEnumerable<string>? bcc = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return false;

                var message = new EmailMessage
                {
                    Subject = subject ?? string.Empty,
                    Body = body ?? string.Empty,
                    BodyFormat = EmailBodyFormat.PlainText
                };

                if (to != null && message.To != null)
                    foreach (var addr in to.Where(a => !string.IsNullOrWhiteSpace(a)))
                        message.To.Add(addr);

                if (cc != null && message.Cc != null)
                    foreach (var addr in cc.Where(a => !string.IsNullOrWhiteSpace(a)))
                        message.Cc.Add(addr);

                if (bcc != null && message.Bcc != null)
                    foreach (var addr in bcc.Where(a => !string.IsNullOrWhiteSpace(a)))
                        message.Bcc.Add(addr);

                if (message.Attachments != null)
                    message.Attachments.Add(new EmailAttachment(filePath));

                await Email.Default.ComposeAsync(message);
                return true;
            }
            catch (FeatureNotSupportedException)
            {
                try
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = subject ?? "Share PDF",
                        File = new ShareFile(filePath)
                    });
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        // Helper: get the top-most MAUI Page to host prompts
        private static Page? GetActivePage()
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
            var page = window?.Page;
            return GetTopPage(page);
        }

        private static Page? GetTopPage(Page? root)
        {
            if (root == null) return null;

            if (root.Navigation?.ModalStack?.Count > 0)
                return root.Navigation.ModalStack.Last();

            return root switch
            {
                NavigationPage nav => GetTopPage(nav.CurrentPage),
                TabbedPage tab => GetTopPage(tab.CurrentPage),
                Shell shell => GetTopPage(shell.CurrentPage),
                _ => root
            };
        }

        private static bool IsValidEmail(string value)
        {
            try
            {
                var addr = new MailAddress(value);
                return addr.Address.Equals(value, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
