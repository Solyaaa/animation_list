using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoListApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TodoTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReminderTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    RepeatInterval = table.Column<string>(type: "TEXT", nullable: false),
                    NextReminder = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramReminders_TelegramUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "TelegramUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TelegramReminders_TodoTasks_TodoTaskId",
                        column: x => x.TodoTaskId,
                        principalTable: "TodoTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramReminders_TelegramUserId_TodoTaskId_ReminderTime",
                table: "TelegramReminders",
                columns: new[] { "TelegramUserId", "TodoTaskId", "ReminderTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramReminders_TodoTaskId",
                table: "TelegramReminders",
                column: "TodoTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramReminders");
        }
    }
}
