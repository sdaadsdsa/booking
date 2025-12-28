using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingSystemAPI.Models
{
    [Table("bookings")]
    public class Booking
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("unique_booking_id")]
        public string UniqueBookingId { get; set; } = string.Empty;

        [Required]
        [Column("guest_full_name")]
        public string GuestFullName { get; set; } = string.Empty;

        [Required]
        [Column("guest_phone")]
        public string GuestPhone { get; set; } = string.Empty;

        [Column("guest_email")]
        public string? GuestEmail { get; set; }

        [Required]
        [Column("booking_topic")]
        public string BookingTopic { get; set; } = string.Empty;

        [Required]
        [Column("booking_date")]
        public DateOnly BookingDate { get; set; }

        [Required]
        [Column("booking_time")]
        public TimeOnly BookingTime { get; set; }

        [Required]
        [Column("duration")]
        public int Duration { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}