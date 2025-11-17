using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCalendar.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiveRemindersToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceiveReminders",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiveReminders",
                table: "AspNetUsers");
        }
    }
}
