namespace BookingSystemAPI.DTOs
{
    public class EmailSettingsDTO
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderPassword { get; set; } = string.Empty;
        public bool UseSSL { get; set; }
    }
}