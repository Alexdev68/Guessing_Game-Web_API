using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuessingGame.API.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApiKeyCreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiKeyHash",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApiKeyHash",
                table: "Users",
                column: "ApiKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ApiKeyHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApiKeyCreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApiKeyHash",
                table: "Users");
        }
    }
}
