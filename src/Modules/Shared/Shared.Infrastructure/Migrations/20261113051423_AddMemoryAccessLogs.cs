using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentMemoryService.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryAccessLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memory_access_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    Query = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_access_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memory_access_logs_AccessedAt",
                table: "memory_access_logs",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_memory_access_logs_AgentId",
                table: "memory_access_logs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_memory_access_logs_MemoryId",
                table: "memory_access_logs",
                column: "MemoryId");

            migrationBuilder.CreateIndex(
                name: "IX_memory_access_logs_NamespaceId",
                table: "memory_access_logs",
                column: "NamespaceId");

            migrationBuilder.CreateIndex(
                name: "IX_memory_access_logs_NamespaceId_AccessedAt",
                table: "memory_access_logs",
                columns: new[] { "NamespaceId", "AccessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_access_logs");
        }
    }
}
