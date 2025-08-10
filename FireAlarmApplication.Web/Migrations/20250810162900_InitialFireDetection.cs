using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace FireAlarmApplication.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialFireDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fire_detection");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "fire_detections",
                schema: "fire_detection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false, comment: "Geographic location using WGS84 coordinate system"),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    Brightness = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    FireRadiativePower = table.Column<double>(type: "double precision", precision: 10, scale: 3, nullable: true),
                    Satellite = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RiskScore = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, defaultValue: 0.0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fire_detections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fire_detections_detected_at",
                schema: "fire_detection",
                table: "fire_detections",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "ix_fire_detections_risk_score",
                schema: "fire_detection",
                table: "fire_detections",
                column: "RiskScore");

            migrationBuilder.CreateIndex(
                name: "ix_fire_detections_status",
                schema: "fire_detection",
                table: "fire_detections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_fire_detections_status_detected_at",
                schema: "fire_detection",
                table: "fire_detections",
                columns: new[] { "Status", "DetectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fire_detections",
                schema: "fire_detection");
        }
    }
}
