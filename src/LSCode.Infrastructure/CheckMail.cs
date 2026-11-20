using System.Net.Sockets;
using LSCode.Contracts;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LSCode.Infrastructure;

internal static class CheckMail
{
    public static async Task<CheckMailResult> SendAsync(CheckMailRequest input, CancellationToken cancellationToken)
    {
        var host = input.Host.Trim();
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            return new(false, "Nhập tên máy chủ hoặc địa chỉ IP hợp lệ.", "host");
        if (input.Port is < 1 or > 65535)
            return new(false, "Cổng phải nằm trong khoảng 1–65535.", "port");

        var security = input.Security switch
        {
            "starttls" => SecureSocketOptions.StartTls,
            "tls" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto
        };
        if (security == SecureSocketOptions.Auto)
            return new(false, "Chọn chế độ bảo mật.", "security");
        if (string.IsNullOrWhiteSpace(input.Account) != string.IsNullOrEmpty(input.Password))
            return new(false, "Nhập cả tài khoản và mật khẩu để xác thực.", string.IsNullOrWhiteSpace(input.Account) ? "account" : "password");
        if (security == SecureSocketOptions.None && input.Password.Length > 0)
            return new(false, "Chọn STARTTLS hoặc TLS để gửi thông tin đăng nhập.", "security");
        if (!MailboxAddress.TryParse(input.From.Trim(), out var sender))
            return new(false, "Email người gửi không hợp lệ.", "from");

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(input.Alias.Trim(), sender.Address));
        foreach (var (field, value, addresses) in new[]
        {
            ("to", input.To, message.To), ("cc", input.Cc, message.Cc), ("bcc", input.Bcc, message.Bcc)
        })
        {
            foreach (var entry in value.Split([';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!MailboxAddress.TryParse(entry, out var address))
                    return new(false, "Địa chỉ email không hợp lệ.", field);
                addresses.Add(address);
            }
        }
        if (message.To.Count == 0)
            return new(false, "Nhập ít nhất một địa chỉ người nhận.", "to");
        message.Subject = input.Subject;
        message.Body = new TextPart(input.Html ? "html" : "plain");
        ((TextPart)message.Body).SetText(System.Text.Encoding.UTF8, input.Body);

        using var client = new SmtpClient();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var stage = "Kết nối SMTP";
        var sending = false;
        try
        {
            await client.ConnectAsync(host, input.Port, security, deadline.Token);
            if (input.Password.Length > 0)
            {
                stage = "Xác thực SMTP";
                await client.AuthenticateAsync(input.Account.Trim(), input.Password, deadline.Token);
            }
            stage = "Gửi thư";
            sending = true;
            var format = FormatOptions.Default.Clone();
            format.HiddenHeaders.Add(HeaderId.Bcc);
            await client.SendAsync(format, new SmtpSendRequest(message), deadline.Token);
            return new(true, "Máy chủ SMTP đã tiếp nhận thư.");
        }
        catch (SslHandshakeException)
        {
            return new(false, "Không thiết lập được TLS. Kiểm tra chế độ bảo mật và chứng chỉ máy chủ.");
        }
        catch (MailKit.Security.AuthenticationException)
        {
            return new(false, "Xác thực SMTP thất bại. Kiểm tra tài khoản và mật khẩu.");
        }
        catch (SmtpCommandException error)
        {
            var detail = error.ErrorCode switch
            {
                SmtpErrorCode.RecipientNotAccepted => $"Máy chủ từ chối người nhận {error.Mailbox?.Address}",
                SmtpErrorCode.SenderNotAccepted => "Máy chủ từ chối người gửi",
                _ => stage
            };
            return new(false, $"{detail} (SMTP {(int)error.StatusCode}).");
        }
        catch (NotSupportedException)
        {
            return new(false, $"{stage} thất bại. Máy chủ không hỗ trợ chế độ đã chọn.");
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or SmtpProtocolException)
        {
            return new(false, sending
                ? "Chưa nhận được xác nhận của SMTP. Kết quả gửi chưa xác định."
                : $"{stage} bị gián đoạn hoặc quá thời gian chờ.");
        }
    }
}
