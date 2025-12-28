namespace BookingSystemAPI.DTOs
{
    public class BookingDTO
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string GuestFullName { get; set; } = string.Empty;
        public string GuestPhone { get; set; } = string.Empty;
        public string? GuestEmail { get; set; }
        public string BookingTopic { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public string BookingTime { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string DisplayDate { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }


    public class CreateBookingDTO
    {
        public string GuestFullName { get; set; } = string.Empty;
        public string GuestPhone { get; set; } = string.Empty;
        public string? GuestEmail { get; set; }
        public string BookingTopic { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public string BookingTime { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string? Comment { get; set; }
    }

    public class AuthDTO
    {
        public string TicketNumber { get; set; } = string.Empty;
        public string GuestFullName { get; set; } = string.Empty;
    }

    public class AdminAuthDTO
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TimeSlotDTO
    {
        public string Time { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }
    // UpdateBookingDTO.cs
    public class UpdateBookingDTO
    {
        public DateOnly? BookingDate { get; set; }
        public string? BookingTime { get; set; }
        public int? Duration { get; set; }
        public string? BookingTopic { get; set; }
        public string? Comment { get; set; }
        public string? ChangeComment { get; set; }
        public string? ChangedByType { get; set; }
    }

    // CancelBookingDTO.cs
    public class CancelBookingDTO
    {
        public string? Comment { get; set; }
        public string? ChangedByType { get; set; }
    }

}