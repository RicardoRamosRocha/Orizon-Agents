using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentExecutionUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentExecutionUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ModelCallCount = table.Column<int>(type: "integer", nullable: false),
                    ToolExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    ToolSuccessCount = table.Column<int>(type: "integer", nullable: false),
                    ToolFailureCount = table.Column<int>(type: "integer", nullable: false),
                    ToolApprovalRequiredCount = table.Column<int>(type: "integer", nullable: false),
                    RagResultCount = table.Column<int>(type: "integer", nullable: false),
                    ToolContextCharacters = table.Column<int>(type: "integer", nullable: false),
                    ContextReductionCharacters = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutionUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentExecutionUsages_AiAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AiAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentExecutionUsages_AiConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionUsages_AgentId",
                table: "AgentExecutionUsages",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionUsages_ConversationId",
                table: "AgentExecutionUsages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionUsages_TenantId_AgentId_StartedAtUtc",
                table: "AgentExecutionUsages",
                columns: new[] { "TenantId", "AgentId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionUsages_TenantId_StartedAtUtc",
                table: "AgentExecutionUsages",
                columns: new[] { "TenantId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentExecutionUsages");
        }
    }
}
