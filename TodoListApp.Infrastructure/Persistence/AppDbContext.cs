using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Domain.Entities;

namespace TodoListApp.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoTask> TodoTasks => Set<TodoTask>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TaskTag> TaskTags => Set<TaskTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TelegramUser> TelegramUsers => Set<TelegramUser>();
    public DbSet<TelegramReminder> TelegramReminders => Set<TelegramReminder>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<TaskTag>().HasKey(tt => new { tt.TodoTaskId, tt.TagId });
        b.Entity<TaskTag>()
            .HasOne(tt => tt.Task)
            .WithMany(t => t.TaskTags)
            .HasForeignKey(tt => tt.TodoTaskId);
        b.Entity<TaskTag>()
            .HasOne(tt => tt.Tag)
            .WithMany(t => t.TaskTags)
            .HasForeignKey(tt => tt.TagId);

        b.Entity<TodoTask>()
            .HasOne(t => t.TodoList)
            .WithMany(l => l.Tasks)
            .HasForeignKey(t => t.TodoListId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Comment>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).IsRequired().HasMaxLength(4000);
            b.HasOne(x => x.TodoTask)
                .WithMany(t => t.Comments)
                .HasForeignKey(x => x.TodoTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TelegramUser>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.TelegramUserId).IsUnique();
            entity.HasIndex(t => t.AppUserId).IsUnique();

            entity.Property(t => t.TelegramUserId)
                .IsRequired();

            entity.Property(t => t.AppUserId)
                .IsRequired()
                .HasMaxLength(450); // Для сумісності з Identity

            // Зв'язок з AppUser
            entity.HasOne(t => t.AppUser)
                .WithMany()
                .HasForeignKey(t => t.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TelegramReminder>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.TelegramUserId, r.TodoTaskId, r.ReminderTime });


        });


        // Додати до методу OnModelCreating
        b.Entity<ApiKey>(entity =>
        {
            entity.HasKey(ak => ak.Id);
            entity.HasIndex(ak => ak.Key).IsUnique();

            entity.Property(ak => ak.Key)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(ak => ak.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(ak => ak.AppUser)
                .WithMany()
                .HasForeignKey(ak => ak.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
