using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentMemoryService.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentNamespaceFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_agent_namespaces_NamespaceId",
                table: "agent_namespaces",
                column: "NamespaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_namespaces_namespaces_NamespaceId",
                table: "agent_namespaces",
                column: "NamespaceId",
                principalTable: "namespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_namespaces_namespaces_NamespaceId",
                table: "agent_namespaces");

            migrationBuilder.DropIndex(
                name: "IX_agent_namespaces_NamespaceId",
                table: "agent_namespaces");
        }
    }
}
