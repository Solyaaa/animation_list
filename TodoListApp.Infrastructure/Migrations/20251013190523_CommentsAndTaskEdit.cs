using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoListApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommentsAndTaskEdit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Comments",
                newName: "CreatedUtc");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedUtc",
                table: "Comments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "Comments");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Comments",
                newName: "CreatedAt");
        }
    }
}
