using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAlarmApplication.Web.UserManagement
{
    /// <inheritdoc />
    public partial class update202509222221 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "user_management");

            //migrationBuilder.AlterDatabase()
            //    .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            //migrationBuilder.CreateTable(
            //    name: "users",
            //    schema: "user_management",
            //    columns: table => new
            //    {
            //        Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
            //        Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            //        PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            //        FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            //        LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            //        Role = table.Column<int>(type: "integer", nullable: false),
            //        HomeLocation = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
            //        CurrentLocation = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
            //        LastLocationUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            //        LocationUpdateFrequencyMinutes = table.Column<int>(type: "integer", nullable: false),
            //        IsLocationTrackingEnabled = table.Column<bool>(type: "boolean", nullable: false),
            //        LocationAccuracy = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
            //        EnableSmsNotification = table.Column<bool>(type: "boolean", nullable: false),
            //        EnableEmailNotification = table.Column<bool>(type: "boolean", nullable: false),
            //        EnablePushNotification = table.Column<bool>(type: "boolean", nullable: false),
            //        PasswordHash = table.Column<string>(type: "text", nullable: false),
            //        RefreshToken = table.Column<string>(type: "text", nullable: true),
            //        RefreshTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            //        FcmToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            //        ApnsToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            //        CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            //        UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            //        IsActive = table.Column<bool>(type: "boolean", nullable: false),
            //        LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_users", x => x.Id);
            //    });

            //migrationBuilder.CreateIndex(
            //    name: "ix_users_active",
            //    schema: "user_management",
            //    table: "users",
            //    column: "IsActive");

            //migrationBuilder.CreateIndex(
            //    name: "ix_users_current_location",
            //    schema: "user_management",
            //    table: "users",
            //    column: "CurrentLocation")
            //    .Annotation("Npgsql:IndexMethod", "GIST");

            //migrationBuilder.CreateIndex(
            //    name: "ix_users_email",
            //    schema: "user_management",
            //    table: "users",
            //    column: "Email",
            //    unique: true);

            //migrationBuilder.CreateIndex(
            //    name: "ix_users_home_location",
            //    schema: "user_management",
            //    table: "users",
            //    column: "HomeLocation")
            //    .Annotation("Npgsql:IndexMethod", "GIST");

            //migrationBuilder.CreateIndex(
            //    name: "ix_users_phone",
            //    schema: "user_management",
            //    table: "users",
            //    column: "PhoneNumber");

            //migrationBuilder.CreateIndex(
            //    name: "ix_users_role",
            //    schema: "user_management",
            //    table: "users",
            //    column: "Role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users",
                schema: "user_management");
        }
    }
}
