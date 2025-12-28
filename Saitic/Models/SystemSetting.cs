using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingSystemAPI.Models
{
    [Table("system_settings")]
    public class SystemSetting
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("setting_key")]
        public string SettingKey { get; set; } = string.Empty;

        [Required]
        [Column("setting_value")]
        public string SettingValue { get; set; } = string.Empty;

        [Column("setting_description")]
        public string? SettingDescription { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}