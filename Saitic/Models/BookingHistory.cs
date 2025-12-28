using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Net; // Добавляем для IPAddress

namespace BookingSystemAPI.Models
{
    [Table("booking_history")]
    public class BookingHistory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("booking_id")]
        public int BookingId { get; set; }

        [Required]
        [Column("changed_by_type")]
        [MaxLength(20)]
        public string ChangedByType { get; set; } = string.Empty;

        [Column("changed_by_id")]
        public int? ChangedById { get; set; }

        [Required]
        [Column("action_type")]
        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [Column("change_details", TypeName = "jsonb")]
        public JsonDocument? ChangeDetails { get; set; }

        [Column("ip_address", TypeName = "inet")] // Указываем тип inet для PostgreSQL
        public IPAddress? IpAddress { get; set; } // Используем IPAddress тип

        [Column("user_agent")]
        public string? UserAgent { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; }
    }
}