using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingSystemAPI.Data;
using BookingSystemAPI.Models;
using BookingSystemAPI.DTOs;
using System.Text.Json;

namespace BookingSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(AppDbContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/bookings/test-date
        [HttpGet("test-date")]
        public ActionResult TestDate([FromQuery] string date)
        {
            Console.WriteLine($"=== ТЕСТ ДАТЫ ===");
            Console.WriteLine($"Дата от фронтенда: '{date}'");

            // Пробуем DateOnly
            if (DateOnly.TryParse(date, out var dateOnly))
            {
                Console.WriteLine($"✅ DateOnly разобрана: {dateOnly:yyyy-MM-dd}");
                Console.WriteLine($"День: {dateOnly.Day}, Месяц: {dateOnly.Month}, Год: {dateOnly.Year}");
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
                Console.WriteLine($"❌ Не удалось разобрать как DateOnly");

                // Пробуем DateTime
                if (DateTime.TryParse(date, out var dateTime))
                {
                    Console.WriteLine($"⚠️ Разобралась как DateTime: {dateTime:yyyy-MM-dd}");
                    Console.WriteLine($"Kind: {dateTime.Kind}, Hour: {dateTime.Hour}, Minute: {dateTime.Minute}");

                    var convertedDateOnly = DateOnly.FromDateTime(dateTime);
                    return Ok(new
                    {
                        success = true,
                        input = date,
                        parsedAsDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        convertedToDateOnly = convertedDateOnly.ToString("yyyy-MM-dd"),
                        suggestion = "Используйте DateOnly.FromDateTime(dateTime)"
                    });
                }
            }

            return BadRequest(new { error = "Не удалось разобрать дату", input = date });
        }

        // GET: api/bookings/available?date=2024-12-15&duration=60
        [HttpGet("available")]
        public async Task<ActionResult<IEnumerable<TimeSlotDTO>>> GetAvailableTimeSlots(
            [FromQuery] string date,
            [FromQuery] int duration = 60)
        {
            try
            {
                Console.WriteLine($"=== ЗАПРОС ДОСТУПНЫХ СЛОТОВ ===");
                Console.WriteLine($"Дата от фронтенда: '{date}'");
                Console.WriteLine($"Длительность: {duration}");

                // Пробуем разные форматы даты
                DateOnly bookingDate;

                if (DateOnly.TryParse(date, out bookingDate))
                {
                    Console.WriteLine($"✅ Дата разобрана как DateOnly: {bookingDate:yyyy-MM-dd}");
                }
                else if (DateTime.TryParse(date, out var dateTime))
                {
                    Console.WriteLine($"⚠️ Дата разобрана как DateTime: {dateTime:yyyy-MM-dd}");
                    bookingDate = DateOnly.FromDateTime(dateTime);
                    Console.WriteLine($"✅ Конвертировано в DateOnly: {bookingDate:yyyy-MM-dd}");
                }
                else
                {
                    Console.WriteLine($"❌ Не удалось разобрать дату: {date}");
                    return BadRequest(new { error = "Неверный формат даты. Используйте формат YYYY-MM-DD" });
                }

                if (duration <= 0 || duration > 480)
                {
                    Console.WriteLine($"❌ Некорректная длительность: {duration}");
                    return BadRequest(new { error = "Длительность должна быть от 1 до 480 минут" });
                }

                Console.WriteLine($"🔍 Поиск слотов для: {bookingDate:yyyy-MM-dd}, длительность: {duration} мин");

                // Конфигурация: минимальный интервал между бронированиями (30 минут)
                const int MIN_INTERVAL_MINUTES = 30;

                var allSlots = GenerateTimeSlots();

                // ВАЖНО: Получаем бронирования ТОЛЬКО для запрошенной даты
                var bookedSlots = await _context.Bookings
                    .Where(b => b.BookingDate == bookingDate && b.IsActive)
                    .ToListAsync();

                Console.WriteLine($"📅 Найдено существующих бронирований на {bookingDate:yyyy-MM-dd}: {bookedSlots.Count}");
                foreach (var booked in bookedSlots)
                {
                    Console.WriteLine($"  - {booked.BookingTime:HH:mm} ({booked.Duration} мин) - {booked.GuestFullName}");
                }

                var result = new List<TimeSlotDTO>();

                foreach (var slot in allSlots)
                {
                    if (!TimeOnly.TryParse(slot, out var slotTime))
                    {
                        Console.WriteLine($"⚠️ Не удалось разобрать время слота: {slot}");
                        continue;
                    }

                    var slotEndTime = slotTime.AddMinutes(duration);

                    // Проверка 1: Не выходит ли за рабочие часы
                    if (slotEndTime > TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)))
                    {
                        // Добавляем как недоступный, но не в результат
                        Console.WriteLine($"❌ Слот выходит за рабочие часы: {slot} ({duration} мин) → {slotEndTime:HH:mm}");
                        continue; // Пропускаем этот слот вообще
                    }

                    bool isAvailable = true;
                    string conflictReason = "";

                    foreach (var booked in bookedSlots)
                    {
                        var bookedStart = booked.BookingTime;
                        var bookedEnd = bookedStart.AddMinutes(booked.Duration);

                        // Расширяем границы существующей брони на MIN_INTERVAL_MINUTES
                        var bookedStartExtended = bookedStart.AddMinutes(-MIN_INTERVAL_MINUTES);
                        var bookedEndExtended = bookedEnd.AddMinutes(MIN_INTERVAL_MINUTES);

                        // Проверка пересечения с расширенными границами
                        if (slotTime < bookedEndExtended && slotEndTime > bookedStartExtended)
                        {
                            isAvailable = false;
                            conflictReason = $"Пересекается с {bookedStart:HH:mm}-{bookedEnd:HH:mm} ({booked.GuestFullName})";
                            break;
                        }
                    }

                    if (isAvailable)
                    {
                        Console.WriteLine($"✅ Слот доступен: {slot} ({duration} мин)");
                        result.Add(new TimeSlotDTO
                        {
                            Time = slot,
                            IsAvailable = true
                        });
                    }
                    else
                    {
                        Console.WriteLine($"❌ Слот занят: {slot} - {conflictReason}");
                        // НЕ добавляем в результат если занят
                    }
                }

                var availableCount = result.Count;
                Console.WriteLine($"📊 Итог: {availableCount} из {allSlots.Count} слотов доступно");
                Console.WriteLine($"=== КОНЕЦ ЗАПРОСА СЛОТОВ ===");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении доступных слотов");
                Console.WriteLine($"💥 ОШИБКА: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"📋 Получены данные:");
                Console.WriteLine($"  ФИО: '{createDto?.GuestFullName}'");
                Console.WriteLine($"  Телефон: '{createDto?.GuestPhone}'");
                Console.WriteLine($"  Тема: '{createDto?.BookingTopic}'");
                Console.WriteLine($"  Дата: '{createDto?.BookingDate}'");
                Console.WriteLine($"  Время: '{createDto?.BookingTime}'");
                Console.WriteLine($"  Длительность: {createDto?.Duration}");
                Console.WriteLine($"  Email: '{createDto?.GuestEmail}'");
                Console.WriteLine($"  Комментарий: '{createDto?.Comment}'");
                Console.WriteLine($"=========================");

                if (createDto == null)
                {
                    Console.WriteLine("❌ Ошибка: DTO пустой");
                    return BadRequest(new { error = "Данные не предоставлены" });
                }

                if (string.IsNullOrWhiteSpace(createDto.GuestFullName))
                {
                    Console.WriteLine("❌ Ошибка: Пустое ФИО");
                    return BadRequest(new { error = "ФИО обязательно" });
                }

                if (string.IsNullOrWhiteSpace(createDto.GuestPhone))
                {
                    Console.WriteLine("❌ Ошибка: Пустой телефон");
                    return BadRequest(new { error = "Телефон обязателен" });
                }

                if (string.IsNullOrWhiteSpace(createDto.BookingTopic))
                {
                    Console.WriteLine("❌ Ошибка: Пустая тема");
                    return BadRequest(new { error = "Тема обязательна" });
                }

                if (createDto.Duration <= 0)
                {
                    Console.WriteLine($"❌ Ошибка: Некорректная длительность: {createDto.Duration}");
                    return BadRequest(new { error = "Длительность должна быть больше 0" });
                }

                if (!TimeOnly.TryParse(createDto.BookingTime, out var startTime))
                {
                    Console.WriteLine($"❌ Ошибка: Неверный формат времени: '{createDto.BookingTime}'");
                    return BadRequest(new { error = "Неверный формат времени. Используйте HH:mm" });
                }

                var bookingDate = createDto.BookingDate;
                var endTime = startTime.AddMinutes(createDto.Duration);

                Console.WriteLine($"🔍 Проверка доступности:");
                Console.WriteLine($"  Дата: {bookingDate:yyyy-MM-dd}");
                Console.WriteLine($"  Время: {startTime:HH:mm} - {endTime:HH:mm}");
                Console.WriteLine($"  Длительность: {createDto.Duration} мин");

                // Конфигурация: минимальный интервал между бронированиями (30 минут)
                const int MIN_INTERVAL_MINUTES = 30;

                var conflictingBooking = await _context.Bookings
                    .Where(b => b.BookingDate == bookingDate && b.IsActive)
                    .Where(b =>
                        // Проверяем пересечение с учетом буфера (30 минут до и после)
                        startTime < b.BookingTime.AddMinutes(b.Duration + MIN_INTERVAL_MINUTES) &&
                        endTime > b.BookingTime.AddMinutes(-MIN_INTERVAL_MINUTES)
                    )
                    .FirstOrDefaultAsync();

                if (conflictingBooking != null)
                {
                    var conflictingEnd = conflictingBooking.BookingTime.AddMinutes(conflictingBooking.Duration);
                    Console.WriteLine($"❌ Время занято на {bookingDate:yyyy-MM-dd}!");
                    Console.WriteLine($"  Конфликтующая бронь: {conflictingBooking.BookingTime:HH:mm}-{conflictingEnd:HH:mm}");
                    Console.WriteLine($"  Клиент: {conflictingBooking.GuestFullName}");

                    return BadRequest(new
                    {
                        error = $"Это время недоступно на {bookingDate:yyyy-MM-dd}. " +
                                $"Между бронированиями должно быть минимум {MIN_INTERVAL_MINUTES} минут. " +
                                $"Ближайшая бронь: {conflictingBooking.BookingTime:HH:mm}-{conflictingEnd:HH:mm}"
                    });
                }

                // Форматирование телефона
                string FormatPhone(string phone)
                {
                    var digits = System.Text.RegularExpressions.Regex.Replace(phone, @"\D", "");
                    if (digits.Length == 10) return $"+7{digits}";
                    if (digits.Length == 11 && digits.StartsWith("7")) return $"+{digits}";
                    if (digits.Length == 11 && digits.StartsWith("8")) return $"+7{digits.Substring(1)}";
                    return $"+{digits}";
                }

                // Создание бронирования
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
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                Console.WriteLine("💾 Сохранение в БД...");
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Обновляем объект
                await _context.Entry(booking).ReloadAsync();

                Console.WriteLine($"✅ Бронирование создано!");
                Console.WriteLine($"  ID: {booking.Id}");
                Console.WriteLine($"  Номер талона: {booking.UniqueBookingId}");
                Console.WriteLine($"  Дата: {booking.BookingDate:yyyy-MM-dd}");
                Console.WriteLine($"  Время: {booking.BookingTime:HH:mm}");
                Console.WriteLine($"  Длительность: {booking.Duration} мин");

                // СОЗДАНИЕ ЗАПИСИ В ИСТОРИИ
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
                            phone = booking.GuestPhone,
                            topic = booking.BookingTopic,
                            date = booking.BookingDate.ToString("yyyy-MM-dd"),
                            time = booking.BookingTime.ToString("HH:mm"),
                            duration = booking.Duration,
                            comment = booking.Comment,
                            createdAt = DateTime.UtcNow.ToString("o")
                        })),
                        IpAddress = null, // Временно null
                        UserAgent = null, // Временно null
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.BookingHistories.Add(history);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"📝 Запись истории создана для бронирования {booking.Id}");
                }
                catch (Exception historyEx)
                {
                    Console.WriteLine($"⚠️ ВНИМАНИЕ: Ошибка при записи истории: {historyEx.Message}");
                    Console.WriteLine($"Бронирование создано, но история не записана");
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

                Console.WriteLine($"✅ Бронирование успешно создано! Номер талона: {bookingDto.TicketNumber}");
                Console.WriteLine($"=== КОНЕЦ СОЗДАНИЯ БРОНИРОВАНИЯ ===");

                return Ok(new
                {
                    success = true,
                    message = "Бронирование успешно создано",
                    ticketNumber = bookingDto.TicketNumber,
                    booking = bookingDto
                });
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"💥 ОШИБКА БАЗЫ ДАННЫХ: {dbEx.Message}");
                Console.WriteLine($"Inner Exception: {dbEx.InnerException?.Message}");
                Console.WriteLine($"StackTrace: {dbEx.StackTrace}");

                return StatusCode(500, new
                {
                    error = "Ошибка при сохранении бронирования",
                    details = dbEx.InnerException?.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ОБЩАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    error = "Внутренняя ошибка сервера",
                    details = ex.Message
                });
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

                if (authDto == null)
                {
                    Console.WriteLine("❌ Ошибка: Данные авторизации не предоставлены");
                    return BadRequest(new { error = "Данные авторизации не предоставлены" });
                }

                if (string.IsNullOrWhiteSpace(authDto.TicketNumber))
                {
                    Console.WriteLine("❌ Ошибка: Номер талона обязателен");
                    return BadRequest(new { error = "Номер талона обязателен" });
                }

                if (string.IsNullOrWhiteSpace(authDto.GuestFullName))
                {
                    Console.WriteLine("❌ Ошибка: ФИО обязательно");
                    return BadRequest(new { error = "ФИО обязательно" });
                }

                var booking = await _context.Bookings
                    .Where(b => b.IsActive)
                    .Where(b => b.UniqueBookingId == authDto.TicketNumber.Trim())
                    .FirstOrDefaultAsync();

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование с номером талона '{authDto.TicketNumber}' не найдено");
                    return NotFound(new { error = "Бронирование с таким номером талона не найдено" });
                }

                if (!string.Equals(booking.GuestFullName.Trim(), authDto.GuestFullName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"❌ Неверное ФИО для талона '{authDto.TicketNumber}'");
                    Console.WriteLine($"   Ожидалось: '{booking.GuestFullName}'");
                    Console.WriteLine($"   Получено: '{authDto.GuestFullName}'");
                    return Unauthorized(new { error = "Неверное ФИО для данного талона" });
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
                Console.WriteLine($"=== КОНЕЦ АВТОРИЗАЦИИ ===");

                return Ok(new
                {
                    success = true,
                    message = "Авторизация успешна",
                    booking = bookingDto
                });
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

                var bookings = await _context.Bookings
                    .Where(b => b.IsActive)
                    .OrderByDescending(b => b.BookingDate)
                    .ThenBy(b => b.BookingTime)
                    .ToListAsync();

                Console.WriteLine($"📋 Найдено бронирований: {bookings.Count}");

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

                Console.WriteLine($"✅ Список бронирований отправлен");
                Console.WriteLine($"=== КОНЕЦ ЗАПРОСА СПИСКА ===");

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
                Console.WriteLine($"=== ПОЛУЧЕНИЕ БРОНИРОВАНИЯ ПО ID ===");
                Console.WriteLine($"ID: {id}");

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
                Console.WriteLine($"=== КОНЕЦ ЗАПРОСА ===");

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
                Console.WriteLine($"Данные для обновления: {JsonSerializer.Serialize(updateDto)}");

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование {id} не найдено");
                    return NotFound(new { error = "Бронирование не найдено" });
                }

                Console.WriteLine($"📋 Текущие данные бронирования:");
                Console.WriteLine($"  ФИО: {booking.GuestFullName}");
                Console.WriteLine($"  Дата: {booking.BookingDate:yyyy-MM-dd}");
                Console.WriteLine($"  Время: {booking.BookingTime:HH:mm}");
                Console.WriteLine($"  Длительность: {booking.Duration} мин");
                Console.WriteLine($"  Тема: {booking.BookingTopic}");

                // Сохраняем старые значения для истории
                var oldBookingDate = booking.BookingDate;
                var oldBookingTime = booking.BookingTime;
                var oldDuration = booking.Duration;
                var oldTopic = booking.BookingTopic;
                var oldComment = booking.Comment;

                // Обновляем поля только если они предоставлены
                bool hasChanges = false;

                DateOnly? newBookingDate = null;
                TimeOnly? newBookingTime = null;
                int? newDuration = null;

                if (updateDto.BookingDate.HasValue && updateDto.BookingDate.Value != booking.BookingDate)
                {
                    newBookingDate = updateDto.BookingDate.Value;
                    booking.BookingDate = newBookingDate.Value;
                    hasChanges = true;
                    Console.WriteLine($"📅 Изменена дата: {oldBookingDate:yyyy-MM-dd} -> {booking.BookingDate:yyyy-MM-dd}");
                }

                if (!string.IsNullOrWhiteSpace(updateDto.BookingTime) &&
                    TimeOnly.TryParse(updateDto.BookingTime, out var parsedTime) &&
                    parsedTime != booking.BookingTime)
                {
                    newBookingTime = parsedTime;
                    booking.BookingTime = parsedTime;
                    hasChanges = true;
                    Console.WriteLine($"⏰ Изменено время: {oldBookingTime:HH:mm} -> {booking.BookingTime:HH:mm}");
                }

                if (updateDto.Duration.HasValue && updateDto.Duration.Value != booking.Duration)
                {
                    if (updateDto.Duration.Value <= 0 || updateDto.Duration.Value > 480)
                    {
                        Console.WriteLine($"❌ Некорректная длительность: {updateDto.Duration.Value}");
                        return BadRequest(new { error = "Длительность должна быть от 1 до 480 минут" });
                    }
                    newDuration = updateDto.Duration.Value;
                    booking.Duration = newDuration.Value;
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
                    return Ok(new { success = true, message = "Нет изменений для сохранения" });
                }

                booking.UpdatedAt = DateTime.UtcNow;

                // Проверяем доступность нового времени, если оно изменилось
                if (hasChanges && (newBookingDate.HasValue || newBookingTime.HasValue))
                {
                    var endTime = booking.BookingTime.AddMinutes(booking.Duration);

                    if (endTime > TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)))
                    {
                        Console.WriteLine($"❌ Бронирование выходит за рабочие часы (после 18:00)");
                        return BadRequest(new { error = "Бронирование выходит за пределы рабочих часов (18:00)" });
                    }

                    // Конфигурация: минимальный интервал между бронированиями (30 минут)
                    const int MIN_INTERVAL_MINUTES = 30;

                    // Проверка пересечений с другими бронированиями С УЧЕТОМ БУФЕРА
                    var conflictingBooking = await _context.Bookings
                        .Where(b => b.BookingDate == booking.BookingDate &&
                                   b.IsActive &&
                                   b.Id != id)
                        .Where(b =>
                            booking.BookingTime < b.BookingTime.AddMinutes(b.Duration + MIN_INTERVAL_MINUTES) &&
                            endTime > b.BookingTime.AddMinutes(-MIN_INTERVAL_MINUTES)
                        )
                        .FirstOrDefaultAsync();

                    if (conflictingBooking != null)
                    {
                        var conflictingEnd = conflictingBooking.BookingTime.AddMinutes(conflictingBooking.Duration);
                        Console.WriteLine($"❌ Время недоступно!");
                        Console.WriteLine($"  Конфликт с: {conflictingBooking.BookingTime:HH:mm}-{conflictingEnd:HH:mm}");
                        Console.WriteLine($"  Клиент: {conflictingBooking.GuestFullName}");

                        return BadRequest(new
                        {
                            error = $"Это время недоступно. Между бронированиями должно быть минимум {MIN_INTERVAL_MINUTES} минут. " +
                                    $"Ближайшая бронь: {conflictingBooking.BookingTime:HH:mm}-{conflictingEnd:HH:mm}"
                        });
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Бронирование {id} успешно обновлено");

                // ЗАПИСЬ В ИСТОРИЮ ПОСЛЕ ОБНОВЛЕНИЯ
                try
                {
                    var history = new BookingHistory
                    {
                        BookingId = booking.Id,
                        ChangedByType = updateDto.ChangedByType ?? "Guest",
                        ActionType = "Update",
                        ChangeDetails = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            action = "Обновление бронирования",
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
                            changeComment = updateDto.ChangeComment,
                            updatedAt = DateTime.UtcNow.ToString("o")
                        })),
                        IpAddress = null, // Временно null
                        UserAgent = null, // Временно null
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.BookingHistories.Add(history);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"📝 Запись истории обновления создана для бронирования {booking.Id}");
                }
                catch (Exception historyEx)
                {
                    Console.WriteLine($"⚠️ ВНИМАНИЕ: Ошибка при записи истории обновления: {historyEx.Message}");
                }

                // Обновляем объект для получения актуальных данных
                await _context.Entry(booking).ReloadAsync();

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

                Console.WriteLine($"✅ Обновление завершено успешно");
                Console.WriteLine($"=== КОНЕЦ ОБНОВЛЕНИЯ ===");

                return Ok(new
                {
                    success = true,
                    message = "Бронирование обновлено",
                    booking = bookingDto
                });
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"💥 ОШИБКА БАЗЫ ДАННЫХ ПРИ ОБНОВЛЕНИИ: {dbEx.Message}");
                Console.WriteLine($"Inner Exception: {dbEx.InnerException?.Message}");

                return StatusCode(500, new
                {
                    error = "Ошибка при обновлении бронирования в базе данных",
                    details = dbEx.InnerException?.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ОБЩАЯ ОШИБКА ПРИ ОБНОВЛЕНИИ: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    error = "Внутренняя ошибка сервера",
                    details = ex.Message
                });
            }
        }

        // DELETE: api/bookings/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> CancelBooking(int id, [FromBody] CancelBookingDTO cancelDto)
        {
            try
            {
                Console.WriteLine($"=== ОТМЕНА БРОНИРОВАНИЯ {id} ===");
                Console.WriteLine($"Причина отмены: '{cancelDto?.Comment}'");
                Console.WriteLine($"Отменяет: '{cancelDto?.ChangedByType}'");

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

                if (booking == null)
                {
                    Console.WriteLine($"❌ Бронирование {id} не найдено");
                    return NotFound(new { error = "Бронирование не найдено" });
                }

                Console.WriteLine($"📋 Бронирование для отмены:");
                Console.WriteLine($"  ФИО: {booking.GuestFullName}");
                Console.WriteLine($"  Дата: {booking.BookingDate:yyyy-MM-dd}");
                Console.WriteLine($"  Время: {booking.BookingTime:HH:mm}");
                Console.WriteLine($"  Тема: {booking.BookingTopic}");

                booking.IsActive = false;
                booking.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Бронирование {id} отменено");

                // ЗАПИСЬ В ИСТОРИЮ ПРИ ОТМЕНЕ
                try
                {
                    var history = new BookingHistory
                    {
                        BookingId = booking.Id,
                        ChangedByType = cancelDto?.ChangedByType ?? "Guest",
                        ActionType = "Cancel",
                        ChangeDetails = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            action = "Отмена бронирования",
                            ticketNumber = booking.UniqueBookingId,
                            guestName = booking.GuestFullName,
                            date = booking.BookingDate.ToString("yyyy-MM-dd"),
                            time = booking.BookingTime.ToString("HH:mm"),
                            reason = cancelDto?.Comment,
                            cancelledAt = DateTime.UtcNow.ToString("o")
                        })),
                        IpAddress = null, // Временно null
                        UserAgent = null, // Временно null
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.BookingHistories.Add(history);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"📝 Запись истории отмены создана для бронирования {booking.Id}");
                }
                catch (Exception historyEx)
                {
                    Console.WriteLine($"⚠️ ВНИМАНИЕ: Ошибка при записи истории отмены: {historyEx.Message}");
                }

                Console.WriteLine($"✅ Отмена завершена успешно");
                Console.WriteLine($"=== КОНЕЦ ОТМЕНЫ ===");

                return Ok(new
                {
                    success = true,
                    message = "Бронирование отменено"
                });
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"💥 ОШИБКА БАЗЫ ДАННЫХ ПРИ ОТМЕНЕ: {dbEx.Message}");
                Console.WriteLine($"Inner Exception: {dbEx.InnerException?.Message}");

                return StatusCode(500, new
                {
                    error = "Ошибка при отмене бронирования",
                    details = dbEx.InnerException?.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ОБЩАЯ ОШИБКА ПРИ ОТМЕНЕ: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    error = "Внутренняя ошибка сервера",
                    details = ex.Message
                });
            }
        }

        // Вспомогательные методы
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

        private async Task<List<Booking>> GetBookedSlotsForDate(DateOnly date)
        {
            return await _context.Bookings
                .Where(b => b.BookingDate == date && b.IsActive)
                .ToListAsync();
        }
    }
}