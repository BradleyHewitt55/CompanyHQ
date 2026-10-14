using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesAndShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fulfillment_origin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    street_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    apartment_unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    suburb = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    city = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    province = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fulfillment_origin", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_counter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_counter", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_coupon",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_automatic = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    applies_to_shipping = table.Column<bool>(type: "boolean", nullable: false),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_coupon", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    source_quote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    guest_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    payment_status = table.Column<int>(type: "integer", nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    shipped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    request_json = table.Column<string>(type: "jsonb", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    shipping_json = table.Column<string>(type: "jsonb", nullable: true),
                    payment_method_name = table.Column<string>(type: "text", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_payment_method",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_payment_method", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_product",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    short_description = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    category_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    supplier_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_product", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_quote",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    converted_to_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    converted_order_number = table.Column<int>(type: "integer", nullable: true),
                    prepared_json = table.Column<string>(type: "jsonb", nullable: false),
                    customer_first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_quote", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    upsell_extra_discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_supplier",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    origin_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_supplier", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipping_method",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    estimated_delivery_days = table.Column<int>(type: "integer", nullable: true),
                    max_distance_km = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    method_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    product_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipping_method", x => x.id);
                    table.CheckConstraint("ck_shipping_method_cost", "cost >= 0");
                });

            migrationBuilder.CreateTable(
                name: "sales_order_upsell_acceptance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    line_json = table.Column<string>(type: "jsonb", nullable: false),
                    priced_line_json = table.Column<string>(type: "jsonb", nullable: false),
                    coupons_json = table.Column<string>(type: "jsonb", nullable: false),
                    shipping_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_order_upsell_acceptance", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_order_upsell_acceptance_sales_order_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_bundle_link",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_bundle_link", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_bundle_link_product_component_product_id",
                        column: x => x.component_product_id,
                        principalTable: "sales_product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_bundle_link_product_product_id",
                        column: x => x.product_id,
                        principalTable: "sales_product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_upsell_offer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_upsell_offer", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_upsell_offer_sales_product_parent_product_id",
                        column: x => x.parent_product_id,
                        principalTable: "sales_product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sales_upsell_offer_sales_product_product_id",
                        column: x => x.product_id,
                        principalTable: "sales_product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_variant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_variant", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_variant_sales_product_product_id",
                        column: x => x.product_id,
                        principalTable: "sales_product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_zone",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipping_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_zone", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_zone_shipping_method_shipping_method_id",
                        column: x => x.shipping_method_id,
                        principalTable: "shipping_method",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipping_method_fulfillment_origin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipping_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_origin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipping_method_fulfillment_origin", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipping_method_fulfillment_origin_fulfillment_origin_fulfi",
                        column: x => x.fulfillment_origin_id,
                        principalTable: "fulfillment_origin",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipping_method_fulfillment_origin_shipping_method_shipping",
                        column: x => x.shipping_method_id,
                        principalTable: "shipping_method",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_variant_selection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_variant_selection", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_variant_selection_catalog_variant_variant_id",
                        column: x => x.variant_id,
                        principalTable: "sales_variant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_zone_postal_code_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    pattern = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_zone_postal_code_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_zone_postal_code_rule_delivery_zone_delivery_zone_",
                        column: x => x.delivery_zone_id,
                        principalTable: "delivery_zone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zone_shipping_method_id",
                table: "delivery_zone",
                column: "shipping_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zone_postal_code_rule_delivery_zone_id",
                table: "delivery_zone_postal_code_rule",
                column: "delivery_zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_bundle_link_component_product_id",
                table: "sales_bundle_link",
                column: "component_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_bundle_link_product_id_component_product_id",
                table: "sales_bundle_link",
                columns: new[] { "product_id", "component_product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_category_slug",
                table: "sales_category",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_coupon_code",
                table: "sales_coupon",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_number",
                table: "sales_order",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_user_id_created_at",
                table: "sales_order",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_upsell_acceptance_order_id_offer_id",
                table: "sales_order_upsell_acceptance",
                columns: new[] { "order_id", "offer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_upsell_acceptance_order_id_request_id",
                table: "sales_order_upsell_acceptance",
                columns: new[] { "order_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_product_is_visible_name",
                table: "sales_product",
                columns: new[] { "is_visible", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_product_slug",
                table: "sales_product",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_quote_converted_to_order_id",
                table: "sales_quote",
                column: "converted_to_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_quote_number",
                table: "sales_quote",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_upsell_offer_parent_product_id_product_id",
                table: "sales_upsell_offer",
                columns: new[] { "parent_product_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_upsell_offer_product_id",
                table: "sales_upsell_offer",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_variant_product_id_sku",
                table: "sales_variant",
                columns: new[] { "product_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_variant_selection_name_value",
                table: "sales_variant_selection",
                columns: new[] { "name", "value" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_variant_selection_variant_id_name",
                table: "sales_variant_selection",
                columns: new[] { "variant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipping_method_is_active_cost",
                table: "shipping_method",
                columns: new[] { "is_active", "cost" });

            migrationBuilder.CreateIndex(
                name: "ix_shipping_method_fulfillment_origin_fulfillment_origin_id",
                table: "shipping_method_fulfillment_origin",
                column: "fulfillment_origin_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipping_method_fulfillment_origin_shipping_method_id_fulfi",
                table: "shipping_method_fulfillment_origin",
                columns: new[] { "shipping_method_id", "fulfillment_origin_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_zone_postal_code_rule");

            migrationBuilder.DropTable(
                name: "sales_bundle_link");

            migrationBuilder.DropTable(
                name: "sales_category");

            migrationBuilder.DropTable(
                name: "sales_counter");

            migrationBuilder.DropTable(
                name: "sales_coupon");

            migrationBuilder.DropTable(
                name: "sales_order_upsell_acceptance");

            migrationBuilder.DropTable(
                name: "sales_payment_method");

            migrationBuilder.DropTable(
                name: "sales_quote");

            migrationBuilder.DropTable(
                name: "sales_settings");

            migrationBuilder.DropTable(
                name: "sales_supplier");

            migrationBuilder.DropTable(
                name: "sales_upsell_offer");

            migrationBuilder.DropTable(
                name: "sales_variant_selection");

            migrationBuilder.DropTable(
                name: "shipping_method_fulfillment_origin");

            migrationBuilder.DropTable(
                name: "delivery_zone");

            migrationBuilder.DropTable(
                name: "sales_order");

            migrationBuilder.DropTable(
                name: "sales_variant");

            migrationBuilder.DropTable(
                name: "fulfillment_origin");

            migrationBuilder.DropTable(
                name: "shipping_method");

            migrationBuilder.DropTable(
                name: "sales_product");
        }
    }
}
