using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCalendar.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderFieldsToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDarkModeEnabled",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "ReminderMinutesBefore",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent",
                table: "Events",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderMinutesBefore",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ReminderSent",
                table: "Events");

            migrationBuilder.AddColumn<bool>(
                name: "IsDarkModeEnabled",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
