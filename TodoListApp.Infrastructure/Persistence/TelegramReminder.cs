// Додайте цей клас до AppDbContext або окремий файл

using System.ComponentModel.DataAnnotations;
using TodoListApp.Domain.Entities;

namespace TodoListApp.Infrastructure.Persistence
{
    public class TelegramReminder
    {
        public int Id { get; set; }

        // ЗМІНІТЬ НА long
        [Required]
        public long TelegramUserId { get; set; } // ← має бути long, як в TelegramUsers

        [Required]
        public int TodoTaskId { get; set; }

        [Required]
        public DateTime ReminderTime { get; set; }

        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }

        public string Message { get; set; } = string.Empty;
        public string RepeatInterval { get; set; } = "none";
        public DateTime? NextReminder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}