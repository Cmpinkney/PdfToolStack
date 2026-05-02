using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfToolStack.Infrastructure.Migrations
{
    public partial class AddPendingBatchJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingBatchJobs",
                columns: table => new
                {
                    PendingBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ToolType = table.Column<int>(type: "int", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    OriginalFileNames = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoredFileReferences = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PendingAccessToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PaymentSessionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingBatchJobs", x => x.PendingBatchId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingBatchJobs_ExpiresAtUtc",
                table: "PendingBatchJobs",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PendingBatchJobs_PaymentSessionId",
                table: "PendingBatchJobs",
                column: "PaymentSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingBatchJobs_PendingAccessToken",
                table: "PendingBatchJobs",
                column: "PendingAccessToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingBatchJobs_Status",
                table: "PendingBatchJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PendingBatchJobs_UserId",
                table: "PendingBatchJobs",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingBatchJobs");
        }
    }
}
