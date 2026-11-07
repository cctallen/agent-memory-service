using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentMemoryService.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgentMultiNamespace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_NamespaceId",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "NamespaceId",
                table: "agents");

            migrationBuilder.CreateTable(
                name: "agent_namespaces",
                columns: table => new
                {
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_namespaces", x => new { x.AgentId, x.NamespaceId });
                    table.ForeignKey(
                        name: "FK_agent_namespaces_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_namespaces");

            migrationBuilder.AddColumn<Guid>(
                name: "NamespaceId",
                table: "agents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_agents_NamespaceId",
                table: "agents",
                column: "NamespaceId");
        }
    }
}
