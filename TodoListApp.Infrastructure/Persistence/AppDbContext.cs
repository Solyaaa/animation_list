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

    }
}
