using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ComputerEquipment.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    Room = table.Column<int>(type: "integer", nullable: false),
                    // xmin — системный столбец PostgreSQL, Npgsql не создает его в таблице.
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment", x => x.Id);
                    table.CheckConstraint("ck_equipment_inventory_number", "\"InventoryNumber\" ~ '^[0-9A-Z]{1,32}$'");
                    table.CheckConstraint("ck_equipment_room", "\"Room\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_equipment_equipment_types_TypeId",
                        column: x => x.TypeId,
                        principalTable: "equipment_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_inventory_number",
                table: "equipment",
                column: "InventoryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_equipment_TypeId",
                table: "equipment",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_types_name",
                table: "equipment_types",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "equipment_types");
        }
    }
}
