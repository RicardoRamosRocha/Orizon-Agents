using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InputSchema = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentToolBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentToolBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentToolBindings_AgentTools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "AgentTools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentToolBindings_AiAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AiAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolBindings_AgentId",
                table: "AgentToolBindings",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolBindings_TenantId_AgentId_IsActive",
                table: "AgentToolBindings",
                columns: new[] { "TenantId", "AgentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolBindings_TenantId_AgentId_ToolId",
                table: "AgentToolBindings",
                columns: new[] { "TenantId", "AgentId", "ToolId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolBindings_ToolId",
                table: "AgentToolBindings",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_TenantId_IsActive",
                table: "AgentTools",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_TenantId_Name",
                table: "AgentTools",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentToolBindings");

            migrationBuilder.DropTable(
                name: "AgentTools");
        }
    }
}
