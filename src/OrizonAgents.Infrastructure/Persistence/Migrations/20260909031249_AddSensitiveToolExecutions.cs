using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSensitiveToolExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ToolExecutionApprovals_TenantId_Id",
                table: "ToolExecutionApprovals",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "SensitiveToolExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentToolBindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProtectedArguments = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ToolConfigurationFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutionStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensitiveToolExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensitiveToolExecutions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensitiveToolExecutions_ToolExecutionApprovals_TenantId_App~",
                        columns: x => new { x.TenantId, x.ApprovalId },
                        principalTable: "ToolExecutionApprovals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveToolExecutions_ExpiresAtUtc",
                table: "SensitiveToolExecutions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveToolExecutions_TenantId_ApprovalId",
                table: "SensitiveToolExecutions",
                columns: new[] { "TenantId", "ApprovalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveToolExecutions_TenantId_State",
                table: "SensitiveToolExecutions",
                columns: new[] { "TenantId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SensitiveToolExecutions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ToolExecutionApprovals_TenantId_Id",
                table: "ToolExecutionApprovals");
        }
    }
}
