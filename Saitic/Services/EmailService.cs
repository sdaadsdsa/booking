using System.Net;
using System.Net.Mail;
using System.Text;

namespace BookingSystemAPI.Services
{
    public interface IEmailService
    {
        Task<bool> SendBookingConfirmation(string guestEmail, string guestName, string ticketNumber, DateTime bookingDateTime, int duration, string topic);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendBookingConfirmation(string guestEmail, string guestName, string ticketNumber, DateTime bookingDateTime, int duration, string topic)
        {
            try
            {
                Console.WriteLine("=== ОТПРАВКА EMAIL ===");
                Console.WriteLine($"Получатель: {guestEmail}");
                Console.WriteLine($"Имя: {guestName}");
                Console.WriteLine($"Талон: {ticketNumber}");
                Console.WriteLine($"Дата/время: {bookingDateTime}");
                Console.WriteLine($"Длительность: {duration}");
                Console.WriteLine($"Тема: {topic}");

                // Читаем настройки
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPort = _configuration["EmailSettings:SmtpPort"];
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderPassword = _configuration["EmailSettings:SenderPassword"];
                var useSSL = _configuration["EmailSettings:UseSSL"];

                Console.WriteLine($"SMTP Server: {smtpServer}");
                Console.WriteLine($"SMTP Port: {smtpPort}");
                Console.WriteLine($"Sender Email: {senderEmail}");
                Console.WriteLine($"UseSSL: {useSSL}");

                // Проверяем настройки
                if (string.IsNullOrEmpty(smtpServer))
                {
                    Console.WriteLine("❌ ОШИБКА: SmtpServer не настроен в appsettings.json");
                    return false;
                }

                if (string.IsNullOrEmpty(senderEmail))
                {
                    Console.WriteLine("❌ ОШИБКА: SenderEmail не настроен в appsettings.json");
                    return false;
                }

                if (string.IsNullOrEmpty(senderPassword))
                {
                    Console.WriteLine("❌ ОШИБКА: SenderPassword не настроен в appsettings.json");
                    return false;
                }

                var port = int.Parse(smtpPort ?? "587");
                var useSsl = bool.Parse(useSSL ?? "true");

                // Форматируем данные
                var dateStr = bookingDateTime.ToString("dd.MM.yyyy");
                var timeStr = bookingDateTime.ToString("HH:mm");

                var durationText = $"{duration} мин.";
                if (duration >= 60)
                {
                    var hours = duration / 60;
                    var minutes = duration % 60;
                    if (minutes == 0)
                        durationText = $"{hours} ч.";
                    else
                        durationText = $"{hours} ч. {minutes} мин.";
                }

                var topicText = topic switch
                {
                    "consultation" => "Консультация",
                    "meeting" => "Встреча",
                    "service" => "Услуга",
                    "training" => "Обучение",
                    _ => topic
                };

                var subject = $"Запись успешно оформлена! Талон: {ticketNumber}";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 0;
            background-color: #f4faf0;
        }}
        .container {{
            max-width: 550px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.1);
            border: 1px solid #c8dfc0;
        }}
        .header {{
            background: linear-gradient(135deg, #9bcd8a 0%, #7bb868 100%);
            padding: 25px 20px;
            text-align: center;
        }}
        .header h1 {{
            color: #2a4a1a;
            font-size: 24px;
            margin: 0;
            font-weight: 700;
        }}
        .content {{
            padding: 30px 25px;
        }}
        .greeting {{
            font-size: 18px;
            color: #3a5a2a;
            margin-bottom: 20px;
        }}
        .greeting strong {{
            color: #2a4a1a;
        }}
        .booking-info {{
            background-color: #e8f3e4;
            border-radius: 12px;
            padding: 20px;
            margin: 20px 0;
        }}
        .info-row {{
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid #c8dfc0;
        }}
        .info-row:last-child {{
            border-bottom: none;
        }}
        .info-label {{
            font-weight: 600;
            color: #6a9a58;
        }}
        .info-value {{
            color: #2a4a1a;
            font-weight: 500;
        }}
        .ticket {{
            background-color: #9bcd8a;
            color: #2a4a1a;
            padding: 8px 15px;
            border-radius: 20px;
            font-weight: bold;
            font-size: 14px;
            display: inline-block;
        }}
        .footer {{
            text-align: center;
            padding: 20px;
            background-color: #f4faf0;
            font-size: 12px;
            color: #8ab87a;
            border-top: 1px solid #c8dfc0;
        }}
        .closing {{
            font-size: 16px;
            color: #3a5a2a;
            margin-top: 20px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ ЗАПИСЬ ОФОРМЛЕНА</h1>
        </div>
        <div class='content'>
            <div class='greeting'>
                Здравствуйте, <strong>{WebUtility.HtmlEncode(guestName)}</strong>!
            </div>
            
            <div class='booking-info'>
                <div class='info-row'>
                    <span class='info-label'>📋 Талон:</span>
                    <span class='info-value'><span class='ticket'>{ticketNumber}</span></span>
                </div>
                <div class='info-row'>
                    <span class='info-label'>📅 Дата:</span>
                    <span class='info-value'>{dateStr}</span>
                </div>
                <div class='info-row'>
                    <span class='info-label'>⏰ Время:</span>
                    <span class='info-value'>{timeStr}</span>
                </div>
                <div class='info-row'>
                    <span class='info-label'>⏱️ Длительность:</span>
                    <span class='info-value'>{durationText}</span>
                </div>
                <div class='info-row'>
                    <span class='info-label'>📌 Тема:</span>
                    <span class='info-value'>{topicText}</span>
                </div>
            </div>
            
            <div class='closing'>
                До встречи! 🤝
            </div>
        </div>
        <div class='footer'>
            <p>Это автоматическое сообщение, пожалуйста, не отвечайте на него.</p>
            <p>© Система бронирования</p>
        </div>
    </div>
</body>
</html>";

                // Отправляем письмо
                using var client = new SmtpClient(smtpServer, port);
                client.EnableSsl = useSsl;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.Timeout = 30000; // 30 секунд таймаут

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "Система бронирования"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(guestEmail);

                Console.WriteLine("📧 Отправка письма...");
                await client.SendMailAsync(mailMessage);

                Console.WriteLine($"✅ Письмо успешно отправлено на {guestEmail}");
                Console.WriteLine("=== КОНЕЦ ОТПРАВКИ EMAIL ===");

                return true;
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"❌ SMTP ОШИБКА: {smtpEx.Message}");
                Console.WriteLine($"StatusCode: {smtpEx.StatusCode}");
                _logger.LogError(smtpEx, $"SMTP ошибка при отправке на {guestEmail}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                _logger.LogError(ex, $"Ошибка отправки email на {guestEmail}");
                return false;
            }
        }
    }
}