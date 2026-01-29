using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAlarmApplication.Web.UserManagement
{
    /// <inheritdoc />
    public partial class InitUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "RefreshTokenExpiry",
                schema: "user_management",
                table: "users",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                schema: "user_management",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                schema: "user_management",
                table: "users");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RefreshTokenExpiry",
                schema: "user_management",
                table: "users",
                type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}


