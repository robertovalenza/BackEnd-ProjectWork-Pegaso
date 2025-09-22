using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Banca_Aurora.Migrations
{
    /// <inheritdoc />
    public partial class AddKeycloakUserIdToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeycloakUserId",
                table: "Customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_KeycloakUserId",
                table: "Customers",
                column: "KeycloakUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_KeycloakUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "KeycloakUserId",
                table: "Customers");
        }
    }
}
