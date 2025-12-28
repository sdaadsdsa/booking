using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingSystemAPI.Data;
using BookingSystemAPI.Models;
using BookingSystemAPI.DTOs;

namespace BookingSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/admin/login
        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] AdminAuthDTO authDto)
        {
            try
            {
                Console.WriteLine("=== АВТОРИЗАЦИЯ ===");
                Console.WriteLine($"Логин: '{authDto?.Username}'");
                Console.WriteLine($"Пароль: '{authDto?.Password}'");

                if (authDto == null || string.IsNullOrEmpty(authDto.Username) || string.IsNullOrEmpty(authDto.Password))
                {
                    Console.WriteLine("Ошибка: Не заполнены поля");
                    return BadRequest(new { error = "Заполните все поля" });
                }

                // Ищем администратора
                var admin = await _context.Admins
                    .FirstOrDefaultAsync(a => a.Username == authDto.Username.Trim());

                if (admin == null)
                {
                    Console.WriteLine($"Админ '{authDto.Username}' не найден");
                    // Посмотрим какие есть админы
                    var allAdmins = await _context.Admins.Select(a => a.Username).ToListAsync();
                    Console.WriteLine($"Все админы: {string.Join(", ", allAdmins)}");
                    return Unauthorized(new { error = "Неверный логин или пароль" });
                }

                Console.WriteLine($"Найден админ: {admin.Username}");
                Console.WriteLine($"Хеш в БД: {admin.PasswordHash}");
                Console.WriteLine($"Активен: {admin.IsActive}");

                if (!admin.IsActive)
                {
                    Console.WriteLine("Админ неактивен");
                    return Unauthorized(new { error = "Аккаунт отключен" });
                }

                // Проверка пароля
                bool passwordValid = false;

                // Сначала пробуем BCrypt
                try
                {
                    Console.WriteLine("Пробую BCrypt...");
                    passwordValid = BCrypt.Net.BCrypt.Verify(authDto.Password, admin.PasswordHash);
                    Console.WriteLine($"BCrypt результат: {passwordValid}");
                }
                catch (Exception bcryptEx)
                {
                    Console.WriteLine($"BCrypt ошибка: {bcryptEx.Message}");

                    // Если BCrypt падает, пробуем прямое сравнение (на случай если пароль в открытом виде)
                    if (authDto.Password == admin.PasswordHash)
                    {
                        Console.WriteLine("Пароль совпал как открытый текст");
                        passwordValid = true;

                        // Обновляем хеш
                        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(authDto.Password, 12);
                        await _context.SaveChangesAsync();
                        Console.WriteLine("Обновил хеш в БД");
                    }
                }

                if (!passwordValid)
                {
                    Console.WriteLine("Пароль неверный");
                    return Unauthorized(new { error = "Неверный логин или пароль" });
                }

                Console.WriteLine("✅ Успешная авторизация!");

                return Ok(new
                {
                    success = true,
                    message = "Вход выполнен успешно",
                    admin = new
                    {
                        id = admin.Id,
                        username = admin.Username,
                        email = admin.Email,
                        fullName = admin.FullName
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        // POST: api/admin/create-test - создание тестового админа
        [HttpPost("create-test")]
        public async Task<ActionResult> CreateTestAdmin()
        {
            try
            {
                // Удаляем если есть
                var existing = await _context.Admins.FirstOrDefaultAsync(a => a.Username == "admin1");
                if (existing != null)
                {
                    _context.Admins.Remove(existing);
                }

                // Создаем нового
                var admin = new Admin
                {
                    Username = "admin1",
                    Email = "admin1@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
                    FullName = "Тестовый Администратор",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Admins.Add(admin);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Тестовый администратор создан",
                    credentials = new
                    {
                        username = "admin1",
                        password = "admin123",
                        hash = admin.PasswordHash
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}