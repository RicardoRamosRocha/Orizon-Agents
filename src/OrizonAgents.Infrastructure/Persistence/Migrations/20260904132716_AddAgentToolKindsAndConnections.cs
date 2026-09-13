using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentToolKindsAndConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationConnectionId",
                table: "AgentTools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "AgentTools",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Http");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_IntegrationConnectionId",
                table: "AgentTools",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_TenantId_IntegrationConnectionId",
                table: "AgentTools",
                columns: new[] { "TenantId", "IntegrationConnectionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgentTools_IntegrationConnections_IntegrationConnectionId",
                table: "AgentTools",
                column: "IntegrationConnectionId",
                principalTable: "IntegrationConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentTools_IntegrationConnections_IntegrationConnectionId",
                table: "AgentTools");

            migrationBuilder.DropIndex(
                name: "IX_AgentTools_IntegrationConnectionId",
                table: "AgentTools");

            migrationBuilder.DropIndex(
                name: "IX_AgentTools_TenantId_IntegrationConnectionId",
                table: "AgentTools");

            migrationBuilder.DropColumn(
                name: "IntegrationConnectionId",
                table: "AgentTools");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AgentTools");
        }
    }
}
