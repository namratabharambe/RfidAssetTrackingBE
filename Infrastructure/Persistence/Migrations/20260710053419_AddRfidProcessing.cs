using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRfidProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "RfidScans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "Readers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActiveTruckSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    GateEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveTruckSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomRfidTags",
                columns: table => new
                {
                    RfidTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomRfidTags", x => x.RfidTagId);
                });

            migrationBuilder.CreateTable(
                name: "GateEvents",
                columns: table => new
                {
                    GateEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEvents", x => x.GateEventId);
                });

            migrationBuilder.CreateTable(
                name: "MissingEquipmentCases",
                columns: table => new
                {
                    MissingEquipmentCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    SeverityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingEquipmentCases", x => x.MissingEquipmentCaseId);
                });

            migrationBuilder.CreateTable(
                name: "MissingEquipmentSeverities",
                columns: table => new
                {
                    SeverityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CostThreshold = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingEquipmentSeverities", x => x.SeverityId);
                });

            migrationBuilder.CreateTable(
                name: "MissingEquipmentStatuses",
                columns: table => new
                {
                    StatusId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingEquipmentStatuses", x => x.StatusId);
                });

            migrationBuilder.CreateTable(
                name: "RfidAlerts",
                columns: table => new
                {
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfidAlerts", x => x.AlertId);
                });

            migrationBuilder.CreateTable(
                name: "TruckEquipmentAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckEquipmentAssignments", x => x.AssignmentId);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastDateTimeOut = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDateTimeIn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RfidTagId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.EquipmentId);
                    table.ForeignKey(
                        name: "FK_Equipment_CustomRfidTags_RfidTagId",
                        column: x => x.RfidTagId,
                        principalTable: "CustomRfidTags",
                        principalColumn: "RfidTagId");
                });

            migrationBuilder.CreateTable(
                name: "Trucks",
                columns: table => new
                {
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckNumber = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfidTagId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trucks", x => x.TruckId);
                    table.ForeignKey(
                        name: "FK_Trucks_CustomRfidTags_RfidTagId",
                        column: x => x.RfidTagId,
                        principalTable: "CustomRfidTags",
                        principalColumn: "RfidTagId");
                });

            migrationBuilder.CreateTable(
                name: "GateEventItems",
                columns: table => new
                {
                    GateEventItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    GateEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epc = table.Column<string>(type: "text", nullable: false),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEventItems", x => x.GateEventItemId);
                    table.ForeignKey(
                        name: "FK_GateEventItems_GateEvents_GateEventId",
                        column: x => x.GateEventId,
                        principalTable: "GateEvents",
                        principalColumn: "GateEventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissingEquipmentCaseItems",
                columns: table => new
                {
                    MissingEquipmentCaseItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissingEquipmentCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epc = table.Column<string>(type: "text", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRecovered = table.Column<bool>(type: "boolean", nullable: false),
                    RecoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingEquipmentCaseItems", x => x.MissingEquipmentCaseItemId);
                    table.ForeignKey(
                        name: "FK_MissingEquipmentCaseItems_MissingEquipmentCases_MissingEqui~",
                        column: x => x.MissingEquipmentCaseId,
                        principalTable: "MissingEquipmentCases",
                        principalColumn: "MissingEquipmentCaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_RfidTagId",
                table: "Equipment",
                column: "RfidTagId");

            migrationBuilder.CreateIndex(
                name: "IX_GateEventItems_GateEventId",
                table: "GateEventItems",
                column: "GateEventId");

            migrationBuilder.CreateIndex(
                name: "IX_MissingEquipmentCaseItems_MissingEquipmentCaseId",
                table: "MissingEquipmentCaseItems",
                column: "MissingEquipmentCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_RfidTagId",
                table: "Trucks",
                column: "RfidTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveTruckSessions");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "GateEventItems");

            migrationBuilder.DropTable(
                name: "MissingEquipmentCaseItems");

            migrationBuilder.DropTable(
                name: "MissingEquipmentSeverities");

            migrationBuilder.DropTable(
                name: "MissingEquipmentStatuses");

            migrationBuilder.DropTable(
                name: "RfidAlerts");

            migrationBuilder.DropTable(
                name: "TruckEquipmentAssignments");

            migrationBuilder.DropTable(
                name: "Trucks");

            migrationBuilder.DropTable(
                name: "GateEvents");

            migrationBuilder.DropTable(
                name: "MissingEquipmentCases");

            migrationBuilder.DropTable(
                name: "CustomRfidTags");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "RfidScans");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "Readers");
        }
    }
}
