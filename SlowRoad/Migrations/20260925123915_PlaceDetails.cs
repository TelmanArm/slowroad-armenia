using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlowRoad.Migrations
{
    /// <inheritdoc />
    public partial class PlaceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BestTime",
                table: "ContentItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "ContentItems",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistanceFromYerevan",
                table: "ContentItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GettingThere",
                table: "ContentItems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Highlights",
                table: "ContentItems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ContentItems",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ContentItems",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeNeeded",
                table: "ContentItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tips",
                table: "ContentItems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestTime",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "DistanceFromYerevan",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "GettingThere",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Highlights",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "TimeNeeded",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Tips",
                table: "ContentItems");
        }
    }
}
