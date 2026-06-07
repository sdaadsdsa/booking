using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingSystemAPI.Data;
using BookingSystemAPI.Models;
using BookingSystemAPI.DTOs;
using BookingSystemAPI.Services;
using System.Text.Json;

namespace BookingSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingsController> _logger;
        private readonly IEmailService _emailService;

        public BookingsController(AppDbContext context, ILogger<BookingsController> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        // GET: api/bookings/test-date
        [HttpGet("test-date")]
        public ActionResult TestDate([FromQuery] string date)
        {
            Console.WriteLine($"=== ТЕСТ ДАТЫ ===");
            Console.WriteLine($"Дата от фронтенда: '{date}'");

            if (DateOnly.TryParse(date, out var dateOnly))
            {
                Console.WriteLine($"✅ DateOnly разобрана: {dateOnly:yyyy-MM-dd}");
                return Ok(new
                {
                    success = true,
                    input = date,
                    parsedDate = dateOnly.ToString("yyyy-MM-dd"),
                    day = dateOnly.Day,
                    month = dateOnly.Month,
                    year = dateOnly.Year,
                    type = "DateOnly"
                });
            }
            else
            {
                if (DateTime.TryParse(date, out var dateTime))
                {
                    var convertedDateOnly = DateOnly.FromDateTime(dateTime);
                    return Ok(new
                    {
                        success = true,
                        input = date,
                        parsedAsDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        convertedToDateOnly = convertedDateOnly.ToString("yyyy-MM-dd")
                    });
                }
            }

            return BadRequest(new { error = "Не удалось разобрать дату", input = date });
        }

        // GET: api/bookings/available
        [HttpGet("available")]
        public async Task<ActionResult<IEnumerable<TimeSlotDTO>>> GetAvailableTimeSlots(
            [FromQuery] string date,
            [FromQuery] int duration = 60)
        {
            try
            {
                Console.WriteLine($"=== ЗАПРОС ДОСТУПНЫХ СЛОТОВ ===");
                Console.WriteLine($"Дата: '{date}', Длительность: {duration}");

                DateOnly bookingDate;

                if (DateOnly.TryParse(date, out bookingDate))
                {
                    Console.WriteLine($"✅ Дата разобрана как DateOnly: {bookingDate:yyyy-MM-dd}");
                }
                else if (DateTime.TryParse(date, out var dateTime))
                {
                    bookingDate = DateOnly.FromDateTime(dateTime);
                    Console.WriteLine($"✅ Конвертировано в DateOnly: {bookingDate:yyyy-MM-dd}");
                }
                else
                {
                    return BadRequest(new { error = "Неверный формат даты. Используйте YYYY-MM-DD" });
                }

                if (duration <= 0 || duration > 480)
                {
                    return BadRequest(new { error = "Длительность должна быть от 1 до 480 минут" });
                }

                const int MIN_INTERVAL_MINUTES = 30;
                var allSlots = GenerateTimeSlots();

                var bookedSlots = await _context.Bookings
                    .Where(b => b.BookingDate == bookingDate && b.IsActive)
                    .ToListAsync();

                var result = new List<TimeSlotDTO>();

                foreach (var slot in allSlots)
                {
                    if (!TimeOnly.TryParse(slot, out var slotTime))
                        continue;

                    var slotEndTime = slotTime.AddMinutes(duration);

                    if (slotEndTime > TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)))
                        continue;

                    bool isAvailable = true;

                    foreach (var booked in bookedSlots)
                    {
                        var bookedStart = booked.BookingTime;
                        var bookedEnd = bookedStart.AddMinutes(booked.Duration);
                        var bookedStartExtended = bookedStart.AddMinutes(-MIN_INTERVAL_MINUTES);
                        var bookedEndExtended = bookedEnd.AddMinutes(MIN_INTERVAL_MINUTES);

                        if (slotTime < bookedEndExtended && slotEndTime > bookedStartExtended)
                        {
                            isAvailable = false;
                            break;
                        }
                    }

                    if (isAvailable)
                    {
                        result.Add(new TimeSlotDTO
                        {
                            Time = slot,
                            IsAvailable = true
                        });
                    }
                }

                Console.WriteLine($"✅ Найдено доступных слотов: {result.Count}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении доступных слотов");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        // POST: api/bookings
        [HttpPost]
        public async Task<ActionResult> CreateBooking([FromBody] CreateBookingDTO createDto)
        {
            try
            {
                Console.WriteLine($"=== НОВОЕ БРОНИРОВАНИЕ ===");
                Console.WriteLine($"ФИО: '{createDto?.GuestFullName}'");
                Console.WriteLine($"Email: '{createDto?.GuestEmail}'");
                Console.WriteLine($"Телефон: '{createDto?.GuestPhone}'");
                Console.WriteLine($"Тема: '{createDto?.BookingTopic}'");
                Console.WriteLine($"Дата: '{createDto?.BookingDate}'");
                Console.WriteLine($"Время: '{createDto?.BookingTime}'");
                Console.WriteLine($"Длительность: {createDto?.Duration}");

                if (createDto == null)
                    return BadRequest(new { error = "Данные не предоставлены" });

                if (string.IsNullOrWhiteSpace(createDto.GuestFullName))
                    return BadRequest(new { error = "ФИО обязательно" });

                if (string.IsNullOrWhiteSpace(createDto.GuestPhone))
                    return BadRequest(new { error = "Телефон обязателен" });

                if (string.IsNullOrWhiteSpace(createDto.GuestEmail))
                    return BadRequest(new { error = "Email обязателен" });

                if (string.IsNullOrWhiteSpace(createDto.BookingTopic))
                    return BadRequest(new { error = "Тема обязательна" });

                if (!TimeOnly.TryParse(createDto.BookingTime, out var startTime))
                    return BadRequest(new { error = "Неверный формат времени" });

                var bookingDate = createDto.BookingDate;
                var endTime = startTime.AddMinutes(createDto.Duration);

                const int MIN_INTERVAL_MINUTES = 30;

                var conflictingBooking = await _context.Bookings
                    .Where(b => b.BookingDate == bookingDate && b.IsActive)
                    .Where(b => startTime < b.BookingTime.AddMinutes(b.Duration + MIN_INTERVAL_MINUTES) &&
                                endTime > b.BookingTime.AddMinutes(-MIN_INTERVAL_MINUTES))
                    .FirstOrDefaultAsync();

                if (conflictingBooking != null)
                {
                    Console.WriteLine($"❌ Время занято! Конфликт с бронированием ID: {conflictingBooking.Id}");
                    return BadRequest(new { error = "Это время уже занято" });
                }

                string FormatPhone(string phone)
                {
                    var digits = System.Text.RegularExpressions.Regex.Replace(phone, @"\D", "");
                    if (digits.Length == 10) return $"+7{digits}";
                    if (digits.Length == 11 && digits.StartsWith("7")) return $"+{digits}";
                    if (digits.Length == 11 && digits.StartsWith("8")) return $"+7{digits.Substring(1)}";
                    return $"+{digits}";
                }

                var booking = new Booking
                {
                    GuestFullName = createDto.GuestFullName.Trim(),
                    GuestPhone = FormatPhone(createDto.GuestPhone),
                    GuestEmail = createDto.GuestEmail?.Trim(),
                    BookingTopic = createDto.BookingTopic.Trim(),
                    BookingDate = bookingDate,
                    BookingTime = startTime,
                    Duration = createDto.Duration,
                    Comment = createDto.Comment?.Trim(),
                    IsActive = true,  // <-- ВАЖНО: явно устанавливаем true
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);

                // Сохраняем и сразу получаем сгенерированный UniqueBookingId
                await _context.SaveChangesAsync();

                // Обновляем запись, чтобы получить UniqueBookingId
                await _context.Entry(booking).ReloadAsync();

                Console.WriteLine($"✅ Бронирование создано!");
                Console.WriteLine($"   ID: {booking.Id}");
                Console.WriteLine($"   Талон: {booking.UniqueBookingId}");
                Console.WriteLine($"   IsActive: {booking.IsActive}");
                Console.WriteLine($"   Дата: {booking.BookingDate}");
                Console.WriteLine($"   Время: {booking.BookingTime}");

                // ========== ОТПРАВКА EMAIL ==========
                try
                {
                    if (!string.IsNullOrEmpty(booking.GuestEmail))
                    {
                        Console.WriteLine($"📧 Пытаюсь отправить email на {booking.GuestEmail}");

                        var bookingDateTime = booking.BookingDate.ToDateTime(booking.BookingTime);

                        var emailSent = await _emailService.SendBookingConfirmation(
                            booking.GuestEmail,
                            booking.GuestFullName,
                            booking.UniqueBookingId,
                            bookingDateTime,
                            booking.Duration,
                            booking.BookingTopic
                        );

                        if (emailSent)
                        {
                            Console.WriteLine($"✅ Email подтверждения отправлен на {booking.GuestEmail}");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Не удалось отправить email на {booking.GuestEmail}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Email не указан для бронирования {booking.Id}");
                    }
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"❌ Ошибка при отправке email: {emailEx.Message}");
                    Console.WriteLine($"StackTrace: {emailEx.StackTrace}");
                }
                // ========== КОНЕЦ ОТПРАВКИ EMAIL ==========

                // Запись в историю
                try
                {
                    var history = new BookingHistory
                    {
                        BookingId = booking.Id,
                        ChangedByType = "Guest",
                        ActionType = "Create",
                        ChangeDetails = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            action = "Создание бронирования",
                            ticketNumber = booking.UniqueBookingId,
                            guestName = booking.GuestFullName,
                            guestEmail = booking.GuestEmail,
                            phone = booking.GuestPhone,
                            date = booking.BookingDate.ToString("yyyy-MM-dd"),
                            time = booking.BookingTime.ToString("HH:mm"),
                            duration = booking.Duration,
                            topic = booking.BookingTopic
                        })),
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.BookingHistories.Add(history);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"📝 История создана для бронирования {booking.Id}");
                }
                catch (Exception historyEx)
                {
                    Console.WriteLine($"⚠️ Ошибка записи истории: {historyEx.Message}");
                }

                var culture = new System.Globalization.CultureInfo("ru-RU");
                var bookingDto = new BookingDTO
                {
                    Id = booking.Id,
                    TicketNumber = booking.UniqueBookingId,
                    GuestFullName = booking.GuestFullName,
                    GuestPhone = booking.GuestPhone,
                    GuestEmail = booking.GuestEmail,
                    BookingTopic = booking.BookingTopic,
                    BookingDate = booking.BookingDate,
                    BookingTime = booking.BookingTime.ToString("HH:mm"),
                    Duration = booking.Duration,
                    Comment = booking.Comment,
                    CreatedAt = booking.CreatedAt,
                    UpdatedAt = booking.UpdatedAt,
                    DisplayDate = booking.BookingDate.ToString("dd MMMM yyyy", culture),
                    EndTime = endTime.ToString("HH:mm"),
                    IsActive = booking.IsActive
                };

                Console.WriteLine($"=== БРОНИРОВАНИЕ УСПЕШНО ЗАВЕРШЕНО ===");

                return Ok(new
                {
                    success = true,
                    message = "Бронирование успешно создано",
                    ticketNumber = bookingDto.TicketNumber,
                    booking = bookingDto
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера", details = ex.Message });
            }
        }

        // POST: api/bookings/auth
        [HttpPost("auth")]
        public async Task<ActionResult> AuthenticateUser([FromBody] AuthDTO authDto)
        {
            try
            {
                Console.WriteLine($"=== АВТОРИЗАЦИЯ ПОЛЬЗОВАТЕЛЯ ===");
                Console.WriteLine($"Номер талона: '{authDto?.TicketNumber}'");
                Console.WriteLine($"ФИО: '{authDto?.GuestFullName}'");

                if (authDto == null || string.IsNullOrWhiteSpace(authDto.TicketNumber))
                    return BadRequest(new { error = "Номер талона обязателен" });

                if (string.IsNullOrWhiteSpace(authDto.GuestFullName))
                    return BadRequest(new { error = "ФИО обязательно" });

                var booking = await _context.Bookings
                    .Where(b => b.IsActive && b.UniqueBookingId == authDto.TicketNumber.Trim())
                    .FirstOrDefaultAsync();

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование с талоном {authDto.TicketNumber} не найдено");
                    return NotFound(new { error = "Бронирование не найдено" });
                }

                if (!string.Equals(booking.GuestFullName.Trim(), authDto.GuestFullName?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"❌ Неверное ФИО для талона {authDto.TicketNumber}");
                    return Unauthorized(new { error = "Неверное ФИО" });
                }

                var culture = new System.Globalization.CultureInfo("ru-RU");
                var bookingDto = new BookingDTO
                {
                    Id = booking.Id,
                    TicketNumber = booking.UniqueBookingId,
                    GuestFullName = booking.GuestFullName,
                    GuestPhone = booking.GuestPhone,
                    GuestEmail = booking.GuestEmail,
                    BookingTopic = booking.BookingTopic,
                    BookingDate = booking.BookingDate,
                    BookingTime = booking.BookingTime.ToString("HH:mm"),
                    Duration = booking.Duration,
                    Comment = booking.Comment,
                    CreatedAt = booking.CreatedAt,
                    UpdatedAt = booking.UpdatedAt,
                    DisplayDate = booking.BookingDate.ToString("dd MMMM yyyy", culture),
                    EndTime = booking.BookingTime.AddMinutes(booking.Duration).ToString("HH:mm"),
                    IsActive = booking.IsActive
                };

                Console.WriteLine($"✅ Авторизация успешна для {booking.GuestFullName}");
                return Ok(new { success = true, booking = bookingDto });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка авторизации: {ex.Message}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        // GET: api/bookings/admin
        [HttpGet("admin")]
        public async Task<ActionResult> GetBookingsAdmin()
        {
            try
            {
                Console.WriteLine($"=== ПОЛУЧЕНИЕ СПИСКА БРОНИРОВАНИЙ (АДМИН) ===");

                // Проверяем какие есть бронирования в БД
                var allBookings = await _context.Bookings.ToListAsync();
                Console.WriteLine($"Всего бронирований в БД: {allBookings.Count}");
                Console.WriteLine($"Активных: {allBookings.Count(b => b.IsActive)}");
                Console.WriteLine($"Неактивных: {allBookings.Count(b => !b.IsActive)}");

                // Берем только активные
                var bookings = await _context.Bookings
                    .Where(b => b.IsActive)
                    .OrderByDescending(b => b.BookingDate)
                    .ThenBy(b => b.BookingTime)
                    .ToListAsync();

                Console.WriteLine($"📋 Активных бронирований: {bookings.Count}");

                foreach (var b in bookings)
                {
                    Console.WriteLine($"   - {b.Id}: {b.GuestFullName}, {b.BookingDate}, {b.BookingTime}, Active={b.IsActive}");
                }

                var culture = new System.Globalization.CultureInfo("ru-RU");
                var result = bookings.Select(b => new BookingDTO
                {
                    Id = b.Id,
                    TicketNumber = b.UniqueBookingId,
                    GuestFullName = b.GuestFullName,
                    GuestPhone = b.GuestPhone,
                    GuestEmail = b.GuestEmail,
                    BookingTopic = b.BookingTopic,
                    BookingDate = b.BookingDate,
                    BookingTime = b.BookingTime.ToString("HH:mm"),
                    Duration = b.Duration,
                    Comment = b.Comment,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
                    DisplayDate = b.BookingDate.ToString("dd MMMM yyyy", culture),
                    EndTime = b.BookingTime.AddMinutes(b.Duration).ToString("HH:mm"),
                    IsActive = b.IsActive
                }).ToList();

                Console.WriteLine($"✅ Отправлено {result.Count} бронирований");
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка получения бронирований: {ex.Message}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        // GET: api/bookings/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult> GetBookingById(int id)
        {
            try
            {
                Console.WriteLine($"=== ПОЛУЧЕНИЕ БРОНИРОВАНИЯ ПО ID: {id} ===");

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование {id} не найдено");
                    return NotFound(new { error = "Бронирование не найдено" });
                }

                var culture = new System.Globalization.CultureInfo("ru-RU");
                var bookingDto = new BookingDTO
                {
                    Id = booking.Id,
                    TicketNumber = booking.UniqueBookingId,
                    GuestFullName = booking.GuestFullName,
                    GuestPhone = booking.GuestPhone,
                    GuestEmail = booking.GuestEmail,
                    BookingTopic = booking.BookingTopic,
                    BookingDate = booking.BookingDate,
                    BookingTime = booking.BookingTime.ToString("HH:mm"),
                    Duration = booking.Duration,
                    Comment = booking.Comment,
                    CreatedAt = booking.CreatedAt,
                    UpdatedAt = booking.UpdatedAt,
                    DisplayDate = booking.BookingDate.ToString("dd MMMM yyyy", culture),
                    EndTime = booking.BookingTime.AddMinutes(booking.Duration).ToString("HH:mm"),
                    IsActive = booking.IsActive
                };

                Console.WriteLine($"✅ Бронирование найдено: {booking.GuestFullName}");
                return Ok(bookingDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка получения бронирования: {ex.Message}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        // PUT: api/bookings/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateBooking(int id, [FromBody] UpdateBookingDTO updateDto)
        {
            try
            {
                Console.WriteLine($"=== ОБНОВЛЕНИЕ БРОНИРОВАНИЯ {id} ===");

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование {id} не найдено");
                    return NotFound(new { error = "Бронирование не найдено" });
                }

                var oldBookingDate = booking.BookingDate;
                var oldBookingTime = booking.BookingTime;
                var oldDuration = booking.Duration;
                var oldTopic = booking.BookingTopic;
                var oldComment = booking.Comment;

                bool hasChanges = false;

                if (updateDto.BookingDate.HasValue && updateDto.BookingDate.Value != booking.BookingDate)
                {
                    booking.BookingDate = updateDto.BookingDate.Value;
                    hasChanges = true;
                    Console.WriteLine($"📅 Изменена дата: {oldBookingDate} -> {booking.BookingDate}");
                }

                if (!string.IsNullOrWhiteSpace(updateDto.BookingTime) &&
                    TimeOnly.TryParse(updateDto.BookingTime, out var parsedTime) &&
                    parsedTime != booking.BookingTime)
                {
                    booking.BookingTime = parsedTime;
                    hasChanges = true;
                    Console.WriteLine($"⏰ Изменено время: {oldBookingTime} -> {booking.BookingTime}");
                }

                if (updateDto.Duration.HasValue && updateDto.Duration.Value != booking.Duration)
                {
                    booking.Duration = updateDto.Duration.Value;
                    hasChanges = true;
                    Console.WriteLine($"⏱️ Изменена длительность: {oldDuration} -> {booking.Duration}");
                }

                if (!string.IsNullOrWhiteSpace(updateDto.BookingTopic) &&
                    updateDto.BookingTopic.Trim() != booking.BookingTopic)
                {
                    booking.BookingTopic = updateDto.BookingTopic.Trim();
                    hasChanges = true;
                    Console.WriteLine($"📝 Изменена тема: {oldTopic} -> {booking.BookingTopic}");
                }

                if (updateDto.Comment != null && updateDto.Comment.Trim() != (booking.Comment ?? ""))
                {
                    booking.Comment = updateDto.Comment.Trim();
                    hasChanges = true;
                    Console.WriteLine($"💬 Изменен комментарий");
                }

                if (!hasChanges)
                {
                    Console.WriteLine($"ℹ️ Нет изменений для сохранения");
                    return Ok(new { success = true, message = "Нет изменений" });
                }

                booking.UpdatedAt = DateTime.UtcNow;

                if (hasChanges && (updateDto.BookingDate.HasValue || !string.IsNullOrWhiteSpace(updateDto.BookingTime)))
                {
                    var endTime = booking.BookingTime.AddMinutes(booking.Duration);

                    if (endTime > TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)))
                        return BadRequest(new { error = "Время выходит за рабочие часы (18:00)" });

                    const int MIN_INTERVAL_MINUTES = 30;

                    var conflictingBooking = await _context.Bookings
                        .Where(b => b.BookingDate == booking.BookingDate && b.IsActive && b.Id != id)
                        .Where(b => booking.BookingTime < b.BookingTime.AddMinutes(b.Duration + MIN_INTERVAL_MINUTES) &&
                                    endTime > b.BookingTime.AddMinutes(-MIN_INTERVAL_MINUTES))
                        .FirstOrDefaultAsync();

                    if (conflictingBooking != null)
                    {
                        Console.WriteLine($"❌ Время конфликтует с бронированием {conflictingBooking.Id}");
                        return BadRequest(new { error = "Это время уже занято" });
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Бронирование {id} обновлено");

                // Запись в историю
                var history = new BookingHistory
                {
                    BookingId = booking.Id,
                    ChangedByType = updateDto.ChangedByType ?? "Guest",
                    ActionType = "Update",
                    ChangeDetails = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        oldValues = new
                        {
                            date = oldBookingDate.ToString("yyyy-MM-dd"),
                            time = oldBookingTime.ToString("HH:mm"),
                            duration = oldDuration,
                            topic = oldTopic,
                            comment = oldComment
                        },
                        newValues = new
                        {
                            date = booking.BookingDate.ToString("yyyy-MM-dd"),
                            time = booking.BookingTime.ToString("HH:mm"),
                            duration = booking.Duration,
                            topic = booking.BookingTopic,
                            comment = booking.Comment
                        },
                        changeComment = updateDto.ChangeComment
                    })),
                    CreatedAt = DateTime.UtcNow
                };

                _context.BookingHistories.Add(history);
                await _context.SaveChangesAsync();

                var culture = new System.Globalization.CultureInfo("ru-RU");
                var bookingDto = new BookingDTO
                {
                    Id = booking.Id,
                    TicketNumber = booking.UniqueBookingId,
                    GuestFullName = booking.GuestFullName,
                    GuestPhone = booking.GuestPhone,
                    GuestEmail = booking.GuestEmail,
                    BookingTopic = booking.BookingTopic,
                    BookingDate = booking.BookingDate,
                    BookingTime = booking.BookingTime.ToString("HH:mm"),
                    Duration = booking.Duration,
                    Comment = booking.Comment,
                    CreatedAt = booking.CreatedAt,
                    UpdatedAt = booking.UpdatedAt,
                    DisplayDate = booking.BookingDate.ToString("dd MMMM yyyy", culture),
                    EndTime = booking.BookingTime.AddMinutes(booking.Duration).ToString("HH:mm"),
                    IsActive = booking.IsActive
                };

                return Ok(new { success = true, message = "Бронирование обновлено", booking = bookingDto });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка обновления: {ex.Message}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        // DELETE: api/bookings/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> CancelBooking(int id, [FromBody] CancelBookingDTO cancelDto)
        {
            try
            {
                Console.WriteLine($"=== ОТМЕНА БРОНИРОВАНИЯ {id} ===");
                Console.WriteLine($"Причина: '{cancelDto?.Comment}'");
                Console.WriteLine($"Кто отменяет: '{cancelDto?.ChangedByType}'");

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование {id} не найдено");
                    return NotFound(new { error = "Бронирование не найдено" });
                }

                booking.IsActive = false;
                booking.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Бронирование {id} отменено");

                // Запись в историю
                var history = new BookingHistory
                {
                    BookingId = booking.Id,
                    ChangedByType = cancelDto?.ChangedByType ?? "Guest",
                    ActionType = "Cancel",
                    ChangeDetails = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        reason = cancelDto?.Comment,
                        cancelledAt = DateTime.UtcNow.ToString("o"),
                        bookingInfo = new
                        {
                            date = booking.BookingDate.ToString("yyyy-MM-dd"),
                            time = booking.BookingTime.ToString("HH:mm"),
                            guestName = booking.GuestFullName
                        }
                    })),
                    CreatedAt = DateTime.UtcNow
                };

                _context.BookingHistories.Add(history);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Бронирование отменено" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка отмены: {ex.Message}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        private List<string> GenerateTimeSlots()
        {
            var slots = new List<string>();
            for (int hour = 9; hour < 18; hour++)
            {
                slots.Add($"{hour:D2}:00");
                if (hour < 17) slots.Add($"{hour:D2}:30");
            }
            return slots;
        }
    }
}