using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DawwerOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReceiptsAndDiscrepancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reservation_group_id",
                table: "stock_reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_activity_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carts", x => x.id);
                    table.ForeignKey(
                        name: "fk_carts_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_carts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name_en = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    city_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_districts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "financial_discrepancies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    actual_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    delta_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewer_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_discrepancies", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_discrepancies_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_financial_discrepancies_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_amendments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    revised_total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amendment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    customer_rejection_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_amendments", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_amendments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_receipt_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_potential_duplicate = table.Column<bool>(type: "boolean", nullable: false),
                    duplicate_of_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewer_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    verified_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_receipt_proofs", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_receipt_proofs_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_payment_receipt_proofs_users_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    repayment_proof_file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    repayment_proof_file_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    repayment_proof_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    repayment_uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    repayment_uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refund_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_refund_tasks_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_cart_items_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cart_items_store_products_product_id",
                        column: x => x.product_id,
                        principalTable: "store_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_delivery_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    district_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flat_delivery_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_order_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    estimated_delivery_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_delivery_zones", x => x.id);
                    table.ForeignKey(
                        name: "fk_merchant_delivery_zones_districts_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_merchant_delivery_zones_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_amendment_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    amendment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    substitute_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    new_quantity = table.Column<int>(type: "integer", nullable: false),
                    new_unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    new_total_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_amendment_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_amendment_items_order_amendments_amendment_id",
                        column: x => x.amendment_id,
                        principalTable: "order_amendments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_reservation_group_id",
                table: "stock_reservations",
                column: "reservation_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_cart_id_product_id",
                table: "cart_items",
                columns: new[] { "cart_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_product_id",
                table: "cart_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_store_id",
                table: "carts",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_id",
                table: "carts",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_districts_city_en",
                table: "districts",
                column: "city_en");

            migrationBuilder.CreateIndex(
                name: "ix_districts_is_active",
                table: "districts",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_financial_discrepancies_order_id",
                table: "financial_discrepancies",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_discrepancies_status",
                table: "financial_discrepancies",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_financial_discrepancies_store_id",
                table: "financial_discrepancies",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_delivery_zones_district_id",
                table: "merchant_delivery_zones",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_delivery_zones_store_id_district_id",
                table: "merchant_delivery_zones",
                columns: new[] { "store_id", "district_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_merchant_delivery_zones_store_id_is_active",
                table: "merchant_delivery_zones",
                columns: new[] { "store_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_order_amendment_items_amendment_id",
                table: "order_amendment_items",
                column: "amendment_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_amendments_expires_at_utc",
                table: "order_amendments",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_order_amendments_order_id",
                table: "order_amendments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_amendments_status",
                table: "order_amendments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipt_proofs_file_hash_sha256",
                table: "payment_receipt_proofs",
                column: "file_hash_sha256");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipt_proofs_order_id",
                table: "payment_receipt_proofs",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipt_proofs_review_decision",
                table: "payment_receipt_proofs",
                column: "review_decision");

            migrationBuilder.CreateIndex(
                name: "ix_payment_receipt_proofs_reviewed_by_id",
                table: "payment_receipt_proofs",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_tasks_order_id",
                table: "refund_tasks",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_tasks_status",
                table: "refund_tasks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "financial_discrepancies");

            migrationBuilder.DropTable(
                name: "merchant_delivery_zones");

            migrationBuilder.DropTable(
                name: "order_amendment_items");

            migrationBuilder.DropTable(
                name: "payment_receipt_proofs");

            migrationBuilder.DropTable(
                name: "refund_tasks");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "districts");

            migrationBuilder.DropTable(
                name: "order_amendments");

            migrationBuilder.DropIndex(
                name: "ix_stock_reservations_reservation_group_id",
                table: "stock_reservations");

            migrationBuilder.DropColumn(
                name: "reservation_group_id",
                table: "stock_reservations");
        }
    }
}
