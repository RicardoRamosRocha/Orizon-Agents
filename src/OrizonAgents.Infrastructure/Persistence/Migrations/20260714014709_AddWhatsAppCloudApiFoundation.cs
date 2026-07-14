using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppCloudApiFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WhatsAppBusinessAccountId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayPhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerifiedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    QualityRating = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastWebhookAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppConnections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppInboxEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInboxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMonthlyUsage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    OutgoingAcceptedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMonthlyUsage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetaMediaId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppMedia_WhatsAppConnections_WhatsAppConnectionId",
                        column: x => x.WhatsAppConnectionId,
                        principalTable: "WhatsAppConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Direction = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Sender = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TextContent = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    MediaId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppMessages_WhatsAppConnections_WhatsAppConnectionId",
                        column: x => x.WhatsAppConnectionId,
                        principalTable: "WhatsAppConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetaTemplateId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ComponentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    LastSynchronizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplates_WhatsAppConnections_WhatsAppConnectionId",
                        column: x => x.WhatsAppConnectionId,
                        principalTable: "WhatsAppConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConnections_PhoneNumberId",
                table: "WhatsAppConnections",
                column: "PhoneNumberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConnections_TenantId_IsDefault",
                table: "WhatsAppConnections",
                columns: new[] { "TenantId", "IsDefault" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConnections_TenantId_Status",
                table: "WhatsAppConnections",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboxEvents_Status_NextAttemptAtUtc",
                table: "WhatsAppInboxEvents",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboxEvents_TenantId_EventId",
                table: "WhatsAppInboxEvents",
                columns: new[] { "TenantId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMedia_TenantId_MetaMediaId",
                table: "WhatsAppMedia",
                columns: new[] { "TenantId", "MetaMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMedia_WhatsAppConnectionId",
                table: "WhatsAppMedia",
                column: "WhatsAppConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_CreatedAtUtc",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_ExternalMessageId",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "ExternalMessageId" },
                unique: true,
                filter: "\"ExternalMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_Status",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_WhatsAppConnectionId",
                table: "WhatsAppMessages",
                column: "WhatsAppConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMonthlyUsage_TenantId_Year_Month",
                table: "WhatsAppMonthlyUsage",
                columns: new[] { "TenantId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOutboxMessages_Status_NextAttemptAtUtc",
                table: "WhatsAppOutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOutboxMessages_TenantId_IdempotencyKey",
                table: "WhatsAppOutboxMessages",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOutboxMessages_WhatsAppMessageId",
                table: "WhatsAppOutboxMessages",
                column: "WhatsAppMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId_Name_Language",
                table: "WhatsAppTemplates",
                columns: new[] { "TenantId", "Name", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_WhatsAppConnectionId_MetaTemplateId",
                table: "WhatsAppTemplates",
                columns: new[] { "WhatsAppConnectionId", "MetaTemplateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppInboxEvents");

            migrationBuilder.DropTable(
                name: "WhatsAppMedia");

            migrationBuilder.DropTable(
                name: "WhatsAppMessages");

            migrationBuilder.DropTable(
                name: "WhatsAppMonthlyUsage");

            migrationBuilder.DropTable(
                name: "WhatsAppOutboxMessages");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplates");

            migrationBuilder.DropTable(
                name: "WhatsAppConnections");
        }
    }
}
