using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DawwerOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "created_at", "created_by", "description", "display_order", "icon_url", "is_active", "name", "parent_category_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0004-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Computers, smartphones, accessories, and electronic appliances.", 1, "/icons/categories/electronics.svg", true, "Electronics", null, null, null },
                    { new Guid("00000000-0000-0000-0004-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Everyday food items, beverages, fresh produce, and pantry staples.", 2, "/icons/categories/groceries.svg", true, "Groceries & Food", null, null, null },
                    { new Guid("00000000-0000-0000-0004-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Clothing, shoes, watches, and fashion accessories.", 3, "/icons/categories/fashion.svg", true, "Fashion & Apparel", null, null, null },
                    { new Guid("00000000-0000-0000-0004-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cookware, home decor, furniture, and kitchen essentials.", 4, "/icons/categories/home.svg", true, "Home & Kitchen", null, null, null },
                    { new Guid("00000000-0000-0000-0004-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Personal care, cosmetics, vitamins, and wellness products.", 5, "/icons/categories/health.svg", true, "Health & Beauty", null, null, null },
                    { new Guid("00000000-0000-0000-0004-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Sporting equipment, fitness gear, and outdoor activity items.", 6, "/icons/categories/sports.svg", true, "Sports & Outdoors", null, null, null },
                    { new Guid("00000000-0000-0000-0004-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Mobile phones, tablets, and wearable accessories.", 1, null, true, "Smartphones & Tablets", new Guid("00000000-0000-0000-0004-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0004-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Personal computers, laptops, monitors, and peripherals.", 2, null, true, "Laptops & Computers", new Guid("00000000-0000-0000-0004-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0004-000000000021"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fruits, vegetables, and fresh herbs.", 1, null, true, "Fresh Produce", new Guid("00000000-0000-0000-0004-000000000002"), null, null },
                    { new Guid("00000000-0000-0000-0004-000000000022"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Water, juices, soft drinks, tea, and coffee.", 2, null, true, "Beverages & Drinks", new Guid("00000000-0000-0000-0004-000000000002"), null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_is_active",
                table: "categories",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_category_id_name",
                table: "categories",
                columns: new[] { "parent_category_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
