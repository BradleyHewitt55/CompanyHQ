using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSalesShippingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "effective_price",
                table: "sales_variant",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_on_sale",
                table: "sales_variant",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "delivery_request_id",
                table: "sales_quote",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quote_reference",
                table: "sales_quote",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                table: "sales_product",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "effective_price",
                table: "sales_product",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_on_sale",
                table: "sales_product",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "price_date",
                table: "sales_product",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<long>(
                name: "price_generation",
                table: "sales_product",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "price_policy_key",
                table: "sales_product",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "rating_total",
                table: "sales_product",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "review_count",
                table: "sales_product",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "units_sold",
                table: "sales_product",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "revision_number",
                table: "sales_order_upsell_acceptance",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "customer_email",
                table: "sales_order",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "customer_first_name",
                table: "sales_order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "customer_last_name",
                table: "sales_order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "invoice_number",
                table: "sales_order",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "invoice_reference",
                table: "sales_order",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "manual_discount_type",
                table: "sales_order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "manual_discount_value",
                table: "sales_order",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "revision_number",
                table: "sales_order",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "invoice_number",
                table: "sales_counter",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "sales_category",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "sales_category",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "sales_category",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "sku",
                table: "sales_category",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_catalog_price_projection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation = table.Column<long>(type: "bigint", nullable: false),
                    last_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dirty = table.Column<bool>(type: "boolean", nullable: false),
                    price_date = table.Column<DateOnly>(type: "date", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_catalog_price_projection", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_revision",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    acting_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_order_revision", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_order_revision_sales_order_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_product_review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    author_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_product_review", x => x.id);
                    table.CheckConstraint("ck_sales_review_rating", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "fk_sales_product_review_sales_product_product_id",
                        column: x => x.product_id,
                        principalTable: "sales_product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_product_average_rating",
                table: "sales_product",
                column: "average_rating");

            migrationBuilder.CreateIndex(
                name: "ix_sales_product_effective_price",
                table: "sales_product",
                column: "effective_price");

            // The earlier schema used the order number as its invoice number. Backfill before
            // enforcing uniqueness so populated databases upgrade just as fresh databases do.
            migrationBuilder.Sql("""
                UPDATE sales_order
                SET invoice_number = number,
                    invoice_reference = 'INV-' || lpad(number::text, greatest(4, length(number::text)), '0'),
                    customer_first_name = COALESCE(request_json #>> '{data,customerFirstName}', request_json ->> 'customerFirstName', ''),
                    customer_last_name = COALESCE(request_json #>> '{data,customerLastName}', request_json ->> 'customerLastName', ''),
                    customer_email = COALESCE(request_json #>> '{data,customerEmail}', request_json ->> 'customerEmail', '');
                UPDATE sales_counter SET invoice_number = number;
                UPDATE sales_quote
                SET quote_reference = 'QUO-' || lpad(number::text, greatest(4, length(number::text)), '0');
                """);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_invoice_number",
                table: "sales_order",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_category_parent_id",
                table: "sales_category",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_revision_order_id_number",
                table: "sales_order_revision",
                columns: new[] { "order_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_product_review_product_id_created_at",
                table: "sales_product_review",
                columns: new[] { "product_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_sales_category_sales_category_parent_id",
                table: "sales_category",
                column: "parent_id",
                principalTable: "sales_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sales_category_sales_category_parent_id",
                table: "sales_category");

            migrationBuilder.DropTable(
                name: "sales_catalog_price_projection");

            migrationBuilder.DropTable(
                name: "sales_order_revision");

            migrationBuilder.DropTable(
                name: "sales_product_review");

            migrationBuilder.DropIndex(
                name: "ix_sales_product_average_rating",
                table: "sales_product");

            migrationBuilder.DropIndex(
                name: "ix_sales_product_effective_price",
                table: "sales_product");

            migrationBuilder.DropIndex(
                name: "ix_sales_order_invoice_number",
                table: "sales_order");

            migrationBuilder.DropIndex(
                name: "ix_sales_category_parent_id",
                table: "sales_category");

            migrationBuilder.DropColumn(
                name: "effective_price",
                table: "sales_variant");

            migrationBuilder.DropColumn(
                name: "is_on_sale",
                table: "sales_variant");

            migrationBuilder.DropColumn(
                name: "delivery_request_id",
                table: "sales_quote");

            migrationBuilder.DropColumn(
                name: "quote_reference",
                table: "sales_quote");

            migrationBuilder.DropColumn(
                name: "average_rating",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "effective_price",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "is_on_sale",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "price_date",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "price_generation",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "price_policy_key",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "rating_total",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "review_count",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "units_sold",
                table: "sales_product");

            migrationBuilder.DropColumn(
                name: "revision_number",
                table: "sales_order_upsell_acceptance");

            migrationBuilder.DropColumn(
                name: "customer_email",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "customer_first_name",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "customer_last_name",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "invoice_reference",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "manual_discount_type",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "manual_discount_value",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "revision_number",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                table: "sales_counter");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "sales_category");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "sales_category");

            migrationBuilder.DropColumn(
                name: "position",
                table: "sales_category");

            migrationBuilder.DropColumn(
                name: "sku",
                table: "sales_category");
        }
    }
}
