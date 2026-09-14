using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DawwerOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAndRoleManagementEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "store_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "store_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_roles_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_role_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    store_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_role_permissions_store_permissions_store_permission_id",
                        column: x => x.store_permission_id,
                        principalTable: "store_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_role_permissions_store_roles_store_role_id",
                        column: x => x.store_role_id,
                        principalTable: "store_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_staff", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_staff_store_roles_store_role_id",
                        column: x => x.store_role_id,
                        principalTable: "store_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_staff_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_staff_users_assigned_by_id",
                        column: x => x.assigned_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_store_staff_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_staff_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    store_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_staff_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_staff_permissions_store_permissions_store_permission_",
                        column: x => x.store_permission_id,
                        principalTable: "store_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_staff_permissions_store_staff_members_store_staff_id",
                        column: x => x.store_staff_id,
                        principalTable: "store_staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "store_permissions",
                columns: new[] { "id", "category", "code", "created_at", "created_by", "description", "name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "Products", "Products.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "View catalog products and product details.", "View Products", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "Products", "Products.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Create, edit, and deactivate store products and categories.", "Manage Products", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "Inventory", "Inventory.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "View store stock levels and inventory availability.", "View Inventory", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "Inventory", "Inventory.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Adjust stock levels, restock items, and set availability thresholds.", "Manage Inventory", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "Orders", "Orders.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "View customer pickup reservations and order history.", "View Orders", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "Orders", "Orders.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Process orders, accept/reject reservations, and update fulfillment states.", "Manage Orders", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "Staff", "Staff.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Add, update, suspend, and remove store staff accounts and assign roles.", "Manage Staff", null, null },
                    { new Guid("00000000-0000-0000-0001-000000000008"), "Store", "Store.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Manage store settings, operating hours, and general configuration.", "Manage Store", null, null }
                });

            migrationBuilder.InsertData(
                table: "store_roles",
                columns: new[] { "id", "created_at", "created_by", "description", "is_system_role", "name", "store_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Full operational and administrative store management access.", true, "Store Manager", null, null, null },
                    { new Guid("00000000-0000-0000-0002-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Manages catalog, inventory, and customer reservations.", true, "Store Supervisor", null, null, null },
                    { new Guid("00000000-0000-0000-0002-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Maintains store stock and catalog entries.", true, "Inventory Clerk", null, null, null },
                    { new Guid("00000000-0000-0000-0002-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Handles pickup orders, customer checkouts, and inventory views.", true, "Cashier", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "store_role_permissions",
                columns: new[] { "id", "created_at", "created_by", "store_permission_id", "store_role_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0003-0001-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000001"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000002"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000003"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000004"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000005"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000006"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000007"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0001-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000008"), new Guid("00000000-0000-0000-0002-000000000001"), null, null },
                    { new Guid("00000000-0000-0003-0002-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000001"), new Guid("00000000-0000-0000-0002-000000000002"), null, null },
                    { new Guid("00000000-0000-0003-0002-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000002"), new Guid("00000000-0000-0000-0002-000000000002"), null, null },
                    { new Guid("00000000-0000-0003-0002-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000003"), new Guid("00000000-0000-0000-0002-000000000002"), null, null },
                    { new Guid("00000000-0000-0003-0002-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000004"), new Guid("00000000-0000-0000-0002-000000000002"), null, null },
                    { new Guid("00000000-0000-0003-0002-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000005"), new Guid("00000000-0000-0000-0002-000000000002"), null, null },
                    { new Guid("00000000-0000-0003-0002-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000006"), new Guid("00000000-0000-0000-0002-000000000002"), null, null },
                    { new Guid("00000000-0000-0003-0003-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000001"), new Guid("00000000-0000-0000-0002-000000000003"), null, null },
                    { new Guid("00000000-0000-0003-0003-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000002"), new Guid("00000000-0000-0000-0002-000000000003"), null, null },
                    { new Guid("00000000-0000-0003-0003-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000003"), new Guid("00000000-0000-0000-0002-000000000003"), null, null },
                    { new Guid("00000000-0000-0003-0003-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000004"), new Guid("00000000-0000-0000-0002-000000000003"), null, null },
                    { new Guid("00000000-0000-0003-0004-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000001"), new Guid("00000000-0000-0000-0002-000000000004"), null, null },
                    { new Guid("00000000-0000-0003-0004-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000003"), new Guid("00000000-0000-0000-0002-000000000004"), null, null },
                    { new Guid("00000000-0000-0003-0004-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000005"), new Guid("00000000-0000-0000-0002-000000000004"), null, null },
                    { new Guid("00000000-0000-0003-0004-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0001-000000000006"), new Guid("00000000-0000-0000-0002-000000000004"), null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_store_permissions_code",
                table: "store_permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_role_permissions_store_permission_id",
                table: "store_role_permissions",
                column: "store_permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_role_permissions_store_role_id_store_permission_id",
                table: "store_role_permissions",
                columns: new[] { "store_role_id", "store_permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_roles_store_id_name",
                table: "store_roles",
                columns: new[] { "store_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_assigned_by_id",
                table: "store_staff",
                column: "assigned_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_status",
                table: "store_staff",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_store_id_user_id",
                table: "store_staff",
                columns: new[] { "store_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_store_role_id",
                table: "store_staff",
                column: "store_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_user_id",
                table: "store_staff",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_permissions_store_permission_id",
                table: "store_staff_permissions",
                column: "store_permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_staff_permissions_store_staff_id_store_permission_id",
                table: "store_staff_permissions",
                columns: new[] { "store_staff_id", "store_permission_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_role_permissions");

            migrationBuilder.DropTable(
                name: "store_staff_permissions");

            migrationBuilder.DropTable(
                name: "store_permissions");

            migrationBuilder.DropTable(
                name: "store_staff");

            migrationBuilder.DropTable(
                name: "store_roles");
        }
    }
}
