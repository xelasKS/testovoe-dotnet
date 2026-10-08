using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientCommissions.Commissions.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "Outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "Events");
        }
    }
}
