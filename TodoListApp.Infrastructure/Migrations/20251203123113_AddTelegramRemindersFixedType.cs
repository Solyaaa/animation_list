using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoListApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramRemindersFixedType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TelegramReminders_TelegramUsers_TelegramUserId",
                table: "TelegramReminders");

            migrationBuilder.DropForeignKey(
                name: "FK_TelegramReminders_TodoTasks_TodoTaskId",
                table: "TelegramReminders");

            migrationBuilder.DropIndex(
                name: "IX_TelegramReminders_TodoTaskId",
                table: "TelegramReminders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TelegramReminders_TodoTaskId",
                table: "TelegramReminders",
                column: "TodoTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_TelegramReminders_TelegramUsers_TelegramUserId",
                table: "TelegramReminders",
                column: "TelegramUserId",
                principalTable: "TelegramUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TelegramReminders_TodoTasks_TodoTaskId",
                table: "TelegramReminders",
                column: "TodoTaskId",
                principalTable: "TodoTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
