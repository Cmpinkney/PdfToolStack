using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfToolStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeProcessedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StripeProcessedEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeProcessedEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StripeProcessedEvents_EventId",
                table: "StripeProcessedEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StripeProcessedEvents_ProcessedAt",
                table: "StripeProcessedEvents",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StripeProcessedEvents");
        }
    }
}
