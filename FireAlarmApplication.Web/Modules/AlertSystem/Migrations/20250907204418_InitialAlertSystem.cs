using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FireAlarmApplication.Web.Modules.AlertSystem.Migrations
{
    /// <inheritdoc />
    public partial class InitialAlertSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "alert_system");

            migrationBuilder.CreateTable(
                name: "alert_rules",
                schema: "alert_system",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetUserRole = table.Column<int>(type: "integer", nullable: false),
                    MinConfidence = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    MaxDistanceKm = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    AllowFeedback = table.Column<bool>(type: "boolean", nullable: false),
                    TitleTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fire_alerts",
                schema: "alert_system",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FireDetectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LocationDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CenterLatitude = table.Column<double>(type: "double precision", precision: 10, scale: 6, nullable: false),
                    CenterLongitude = table.Column<double>(type: "double precision", precision: 10, scale: 6, nullable: false),
                    MaxRadiusKm = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    OriginalConfidence = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    PositiveFeedbackCount = table.Column<int>(type: "integer", nullable: false),
                    NegativeFeedbackCount = table.Column<int>(type: "integer", nullable: false),
                    FeedbackSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastFeedbackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fire_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alert_feedbacks",
                schema: "alert_system",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FireAlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    UserLatitude = table.Column<double>(type: "double precision", precision: 10, scale: 6, nullable: false),
                    UserLongitude = table.Column<double>(type: "double precision", precision: 10, scale: 6, nullable: false),
                    DistanceToFireKm = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    ReliabilityScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    ConfidenceImpact = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, defaultValue: 0.0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_feedbacks_fire_alerts_FireAlertId",
                        column: x => x.FireAlertId,
                        principalSchema: "alert_system",
                        principalTable: "fire_alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_alerts",
                schema: "alert_system",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FireAlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserRole = table.Column<int>(type: "integer", nullable: false),
                    UserLatitude = table.Column<double>(type: "double precision", precision: 10, scale: 6, nullable: false),
                    UserLongitude = table.Column<double>(type: "double precision", precision: 10, scale: 6, nullable: false),
                    DistanceToFireKm = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    AlertMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CanProvideFeedBack = table.Column<bool>(type: "boolean", nullable: false),
                    IsDelivered = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_alerts_fire_alerts_FireAlertId",
                        column: x => x.FireAlertId,
                        principalSchema: "alert_system",
                        principalTable: "fire_alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "alert_system",
                table: "alert_rules",
                columns: new[] { "Id", "AllowFeedback", "CreatedAt", "Description", "IsActive", "MaxDistanceKm", "MessageTemplate", "MinConfidence", "Name", "TargetUserRole", "TitleTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2025, 9, 7, 20, 44, 18, 438, DateTimeKind.Utc).AddTicks(7520), "Vatandaşlar için yakın yangın uyarıları", true, 15.0, "{Distance}km uzağınızda yangın tespit edildi. Güvenirlik: %{Confidence}. Dikkatli olun.", 50.0, "Vatandaş - Yakın Mesafe", 0, "DİKKAT: Yangın Tespiti - {Location}", null },
                    { 2, false, new DateTime(2025, 9, 7, 20, 44, 18, 438, DateTimeKind.Utc).AddTicks(7525), "Orman görevlileri için tüm şüpheli tespitler", true, 50.0, "{Distance}km mesafede şüpheli yangın tespiti. Güvenirlik: %{Confidence}. Kontrol edilmesi gerekiyor.", 30.0, "Orman Görevlisi - Geniş Alan", 1, "Şüpheli Yangın Tespiti - {Location}", null },
                    { 3, false, new DateTime(2025, 9, 7, 20, 44, 18, 438, DateTimeKind.Utc).AddTicks(7527), "İtfaiye için yüksek güvenirlikli yangınlar", true, 100.0, "{Distance}km mesafede yangın tespit edildi. Güvenirlik: %{Confidence}. Acil müdahale gerekli.", 60.0, "İtfaiye - Acil Müdahale", 2, "ACİL: Yangın Müdahale - {Location}", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_feedbacks_alert_user",
                schema: "alert_system",
                table: "alert_feedbacks",
                columns: new[] { "FireAlertId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alert_feedbacks_fire_alert_id",
                schema: "alert_system",
                table: "alert_feedbacks",
                column: "FireAlertId");

            migrationBuilder.CreateIndex(
                name: "ix_alert_feedbacks_type",
                schema: "alert_system",
                table: "alert_feedbacks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_alert_feedbacks_user_id",
                schema: "alert_system",
                table: "alert_feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_active",
                schema: "alert_system",
                table: "alert_rules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_role_active",
                schema: "alert_system",
                table: "alert_rules",
                columns: new[] { "TargetUserRole", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_user_role",
                schema: "alert_system",
                table: "alert_rules",
                column: "TargetUserRole");

            migrationBuilder.CreateIndex(
                name: "ix_fire_alerts_fire_detection_id",
                schema: "alert_system",
                table: "fire_alerts",
                column: "FireDetectionId");

            migrationBuilder.CreateIndex(
                name: "ix_fire_alerts_location",
                schema: "alert_system",
                table: "fire_alerts",
                columns: new[] { "CenterLatitude", "CenterLongitude" });

            migrationBuilder.CreateIndex(
                name: "ix_fire_alerts_status",
                schema: "alert_system",
                table: "fire_alerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_fire_alerts_status_expires",
                schema: "alert_system",
                table: "fire_alerts",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_user_alerts_delivery_status",
                schema: "alert_system",
                table: "user_alerts",
                columns: new[] { "IsDelivered", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_user_alerts_fire_alert_id",
                schema: "alert_system",
                table: "user_alerts",
                column: "FireAlertId");

            migrationBuilder.CreateIndex(
                name: "ix_user_alerts_user_fire",
                schema: "alert_system",
                table: "user_alerts",
                columns: new[] { "UserId", "FireAlertId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_alerts_user_id",
                schema: "alert_system",
                table: "user_alerts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_feedbacks",
                schema: "alert_system");

            migrationBuilder.DropTable(
                name: "alert_rules",
                schema: "alert_system");

            migrationBuilder.DropTable(
                name: "user_alerts",
                schema: "alert_system");

            migrationBuilder.DropTable(
                name: "fire_alerts",
                schema: "alert_system");
        }
    }
}
