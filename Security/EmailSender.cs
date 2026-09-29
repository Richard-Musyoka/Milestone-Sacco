using System.Net;
using System.Net.Mail;
using System.Net.Sockets;

namespace SaccoManagementSystem.Security;

public sealed record SendResult(bool Ok, string? Error = null);

public interface IOtpSender
{
    /// <summary>purpose: "signin" or "reset". Never throws: failures come back in the result.</summary>
    Task<SendResult> SendAsync(string channel, string destination, string code, string purpose = "signin", string? name = null);
}

/// <summary>
/// Sends one-time codes by email over SMTP using the "EmailSettings" configuration section
/// (SmtpServer, SmtpPort, SenderEmail, SenderName, Username, Password, EnableSsl, EnableStartTls, UseDefaultCredentials).
/// SMS is not wired to a gateway yet, so SMS requests report a clear failure.
/// </summary>
public sealed class SmtpOtpSender : IOtpSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<SmtpOtpSender> _log;
    public SmtpOtpSender(IConfiguration cfg, ILogger<SmtpOtpSender> log) { _cfg = cfg; _log = log; }

    private IConfigurationSection S => _cfg.GetSection("EmailSettings");
    private string Brand => _cfg["Brand:Name"] ?? "Tajiri Sacco";

    public bool Configured => !string.IsNullOrWhiteSpace(S["SmtpServer"]) && !string.IsNullOrWhiteSpace(S["SenderEmail"]);

    public async Task<SendResult> SendAsync(string channel, string destination, string code, string purpose = "signin", string? name = null)
    {
        if (channel == "sms")
            return await SendSmsAsync(destination, $"{Brand}: your {(purpose == "reset" ? "reset" : "sign-in")} code is {code}. It expires in {(purpose == "reset" ? 15 : 10)} minutes. Never share it - staff will never ask for it.");
        if (channel != "email") return new SendResult(false, "Unknown delivery channel.");
        var reset = purpose == "reset";
        var subject = reset ? $"{Brand} password reset code: {code}" : $"{Brand} sign-in code: {code}";
        var title = reset ? "Reset your password" : "Your sign-in code";
        var lead = reset ? "Use this code to choose a new password." : "Use this code to finish signing in.";
        var minutes = reset ? 15 : 10;
        return await SendMailAsync(destination, subject, Template(title, name, lead, code, minutes));
    }

    // ------------------------------------------------------------------ SMS (Africa's Talking)
    private static readonly HttpClient SmsHttp = new() { Timeout = TimeSpan.FromSeconds(20) };
    private IConfigurationSection Sms => _cfg.GetSection("Sms");
    public bool SmsConfigured => !string.IsNullOrWhiteSpace(Sms["Username"]) && !string.IsNullOrWhiteSpace(Sms["ApiKey"]);

    /// <summary>Kenyan numbers are normalised to +2547XXXXXXXX / +2541XXXXXXXX.</summary>
    public static string KenyaE164(string phone)
    {
        var d = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (d.StartsWith("254") && d.Length == 12) return "+" + d;
        if (d.StartsWith("0") && d.Length == 10) return "+254" + d[1..];
        if (d.Length == 9 && (d[0] == '7' || d[0] == '1')) return "+254" + d;
        return (phone ?? "").StartsWith("+") ? "+" + d : "+" + d;
    }

    /// <summary>
    /// Sends a text through Africa's Talking when the "Sms" section is filled in:
    /// { "Sms": { "Username": "sandbox", "ApiKey": "...", "SenderId": "", "Sandbox": true } }.
    /// </summary>
    public async Task<SendResult> SendSmsAsync(string phone, string message)
    {
        if (!SmsConfigured) return new SendResult(false, "SMS isn't set up. Add an \"Sms\" section (Africa's Talking username and API key) to appsettings.json.");
        try
        {
            var sandbox = Sms.GetValue<bool>("Sandbox") || Sms["Username"] == "sandbox";
            var url = sandbox ? "https://api.sandbox.africastalking.com/version1/messaging" : "https://api.africastalking.com/version1/messaging";
            var form = new Dictionary<string, string> { ["username"] = Sms["Username"]!, ["to"] = KenyaE164(phone), ["message"] = message };
            if (!string.IsNullOrWhiteSpace(Sms["SenderId"])) form["from"] = Sms["SenderId"]!;
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
            req.Headers.Add("apiKey", Sms["ApiKey"]);
            req.Headers.Add("Accept", "application/json");
            using var res = await SmsHttp.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode || body.Contains("\"InvalidPhoneNumber\"") || body.Contains("\"InsufficientBalance\""))
            {
                _log.LogWarning("SMS send failed ({Status}): {Body}", (int)res.StatusCode, body);
                return new SendResult(false, "The SMS gateway refused the message.");
            }
            return new SendResult(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SMS send failed");
            return new SendResult(false, "Couldn't reach the SMS gateway.");
        }
    }

    public async Task<SendResult> SendMailAsync(string to, string subject, string htmlBody)
    {
        if (!Configured) return new SendResult(false, "Email isn't configured. Add an \"EmailSettings\" section to appsettings.json.");
        try
        {
            var host = S["SmtpServer"]!;
            var port = S.GetValue<int?>("SmtpPort") ?? 587;
            bool ssl = S.GetValue<bool?>("EnableSsl") ?? true;
            bool startTls = S.GetValue<bool?>("EnableStartTls") ?? true;
            var sender = S["SenderEmail"]!;
            var display = _cfg["Brand:Name"] ?? S["SenderName"] ?? sender;

            using var msg = new MailMessage { From = new MailAddress(sender, display), Subject = subject, Body = htmlBody, IsBodyHtml = true };
            msg.To.Add(new MailAddress(to));

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = ssl || startTls,       // System.Net.Mail negotiates STARTTLS on 587 when this is true
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000,
                UseDefaultCredentials = S.GetValue<bool?>("UseDefaultCredentials") ?? false,
            };
            if (!client.UseDefaultCredentials && !string.IsNullOrEmpty(S["Username"]))
                client.Credentials = new NetworkCredential(S["Username"], (S["Password"] ?? "").Replace(" ", ""));   // Google shows app passwords with spaces

            await client.SendMailAsync(msg);
            return new SendResult(true);
        }
        catch (SmtpException ex)
        {
            _log.LogError(ex, "SMTP send failed ({Status})", ex.StatusCode);
            return new SendResult(false, ex.StatusCode switch
            {
                SmtpStatusCode.MustIssueStartTlsFirst => "The mail server requires TLS. Check EnableSsl / EnableStartTls.",
                SmtpStatusCode.ClientNotPermitted or SmtpStatusCode.MailboxUnavailable => "The mail server rejected the sender or recipient. For Gmail use an App Password.",
                _ when ex.Message.Contains("5.7.0") || ex.Message.Contains("Authentication", StringComparison.OrdinalIgnoreCase) => "The mail server rejected the username or password. For Gmail use an App Password.",
                _ => "The mail server couldn't send the message (" + ex.StatusCode + ")."
            });
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or InvalidOperationException)
        {
            _log.LogError(ex, "SMTP connection failed");
            return new SendResult(false, "Couldn't reach the mail server. Check SmtpServer, SmtpPort and the network.");
        }
        catch (FormatException ex)
        {
            _log.LogError(ex, "Bad email address");
            return new SendResult(false, "The email address isn't valid.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected email failure");
            return new SendResult(false, "Unexpected email failure: " + ex.Message);
        }
    }

    private string Template(string title, string? name, string lead, string code, int minutes)
    {
        var hello = string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {WebUtility.HtmlEncode(name.Split(' ')[0])},";
        var brand = WebUtility.HtmlEncode(Brand);
        return $@"<!doctype html><html><body style=""margin:0;background:#f4f6f9;font-family:Segoe UI,Arial,sans-serif;color:#0f172a"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:32px 12px"">
<table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e3e8ef"">
<tr><td style=""background:#0B1F3A;padding:22px 28px;color:#ffffff;font-size:18px;font-weight:700"">{brand}</td></tr>
<tr><td style=""padding:28px"">
<h2 style=""margin:0 0 8px;font-size:22px"">{WebUtility.HtmlEncode(title)}</h2>
<p style=""margin:0 0 18px;color:#475569;font-size:14.5px;line-height:1.5"">{hello} {WebUtility.HtmlEncode(lead)}</p>
<div style=""text-align:center;background:#E3F4EC;border-radius:12px;padding:18px;font-size:34px;font-weight:800;letter-spacing:10px;color:#0B7A52"">{code}</div>
<p style=""margin:18px 0 0;color:#64748B;font-size:13px;line-height:1.5"">This code expires in {minutes} minutes and works once. If you didn't ask for it, you can ignore this email — your password is still safe.</p>
</td></tr>
<tr><td style=""padding:14px 28px;background:#f8fafc;color:#94a3b8;font-size:12px"">Sent by {brand}. Never share this code with anyone.</td></tr>
</table></td></tr></table></body></html>";
    }
}
