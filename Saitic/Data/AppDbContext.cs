using Microsoft.EntityFrameworkCore;
using BookingSystemAPI.Models;

namespace BookingSystemAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<BookingHistory> BookingHistories => Set<BookingHistory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Значения по умолчанию как в триггерах БД
            modelBuilder.Entity<Booking>()
                .Property(b => b.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Booking>()
                .Property(b => b.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Booking>()
                .Property(b => b.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Admin>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Admin>()
                .Property(a => a.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<SystemSetting>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<SystemSetting>()
                .Property(s => s.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<BookingHistory>()
                .Property(h => h.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Уникальные ограничения
            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.UniqueBookingId)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Username)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Email)
                .IsUnique();

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(s => s.SettingKey)
                .IsUnique();
        }
    }
}