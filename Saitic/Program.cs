using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using BookingSystemAPI.Data;
using BookingSystemAPI.Models;
using BookingSystemAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// Подключение к БД
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрация сервисов - ВОТ ЗДЕСЬ нужно добавить!
builder.Services.AddScoped<IEmailService, EmailService>(); // Исправь IEmaillService на IEmailService

// Добавляем логирование
builder.Services.AddLogging();

var app = builder.Build();

// Используем статические файлы
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = ""
});

app.MapGet("/", () => Results.Redirect("/index.html"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Booking API v1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Проверка подключения к БД
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.EnsureCreatedAsync();
        var canConnect = await db.Database.CanConnectAsync();

        if (canConnect)
        {
            logger.LogInformation("✅ Успешно подключено к БД 'sites'");

            var adminExists = await db.Admins.AnyAsync();
            if (!adminExists)
            {
                logger.LogWarning("⚠️ Администраторы не найдены в базе данных");
            }
        }
        else
        {
            logger.LogError("❌ Не удалось подключиться к БД");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Ошибка подключения к БД");
    }
}

// Создание администратора
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var existingAdmin = await db.Admins.FirstOrDefaultAsync(a => a.Username == "admin1");

        if (existingAdmin == null)
        {
            logger.LogInformation("Создание администратора admin1...");

            var admin = new Admin
            {
                Username = "admin1",
                Email = "admin1@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
                FullName = "Администратор Системы",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Admins.Add(admin);
            await db.SaveChangesAsync();

            logger.LogInformation("✅ Администратор admin1 создан!");
            logger.LogInformation($"   Логин: admin1");
            logger.LogInformation($"   Пароль: admin123");
        }
        else
        {
            logger.LogInformation($"Найден администратор: {existingAdmin.Username}");

            try
            {
                bool isValid = BCrypt.Net.BCrypt.Verify("admin123", existingAdmin.PasswordHash);
                logger.LogInformation($"   Пароль валиден: {isValid}");

                if (!isValid)
                {
                    logger.LogWarning("   ⚠️ Пароль невалиден. Обновляю...");
                    existingAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12);
                    await db.SaveChangesAsync();
                    logger.LogInformation("   ✅ Пароль обновлен!");
                }
            }
            catch (Exception hashEx)
            {
                logger.LogWarning($"   ⚠️ Ошибка проверки пароля: {hashEx.Message}");
                logger.LogWarning("   Обновляю пароль...");
                existingAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12);
                await db.SaveChangesAsync();
                logger.LogInformation("   ✅ Пароль обновлен!");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при работе с администратором");
    }
}

app.Run();