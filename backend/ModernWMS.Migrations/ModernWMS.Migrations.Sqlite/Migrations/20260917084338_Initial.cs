using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModernWMS.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "action_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    vue_path = table.Column<string>(type: "TEXT", nullable: false),
                    user_name = table.Column<string>(type: "TEXT", nullable: false),
                    action_content = table.Column<string>(type: "TEXT", nullable: false),
                    action_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asnmaster",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    asn_no = table.Column<string>(type: "TEXT", nullable: false),
                    asn_batch = table.Column<string>(type: "TEXT", nullable: false),
                    estimated_arrival_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    asn_status = table.Column<byte>(type: "INTEGER", nullable: false),
                    weight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_name = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asnmaster", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asnsort",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    asn_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sorted_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    putaway_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asnsort", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "category",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    category_name = table.Column<string>(type: "TEXT", nullable: false),
                    parent_id = table.Column<int>(type: "INTEGER", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    company_name = table.Column<string>(type: "TEXT", nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: false),
                    manager = table.Column<string>(type: "TEXT", nullable: false),
                    contact_tel = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    customer_name = table.Column<string>(type: "TEXT", nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    manager = table.Column<string>(type: "TEXT", nullable: false),
                    contact_tel = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispatchlist",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    dispatch_no = table.Column<string>(type: "TEXT", nullable: false),
                    dispatch_status = table.Column<byte>(type: "INTEGER", nullable: false),
                    customer_id = table.Column<int>(type: "INTEGER", nullable: false),
                    customer_name = table.Column<string>(type: "TEXT", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    qty = table.Column<int>(type: "INTEGER", nullable: false),
                    weight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    damage_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    lock_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    picked_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    intrasit_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    package_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    weighing_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    actual_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    sign_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    package_no = table.Column<string>(type: "TEXT", nullable: false),
                    package_person = table.Column<string>(type: "TEXT", nullable: false),
                    package_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    weighing_no = table.Column<string>(type: "TEXT", nullable: false),
                    weighing_person = table.Column<string>(type: "TEXT", nullable: false),
                    weighing_weight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    waybill_no = table.Column<string>(type: "TEXT", nullable: false),
                    carrier = table.Column<string>(type: "TEXT", nullable: false),
                    freightfee = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    pick_checker_id = table.Column<int>(type: "INTEGER", nullable: false),
                    pick_checker = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatchlist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flowsetmain",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    menu = table.Column<string>(type: "TEXT", nullable: false),
                    flow_name = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flowsetmain", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "freightfee",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    carrier = table.Column<string>(type: "TEXT", nullable: false),
                    departure_city = table.Column<string>(type: "TEXT", nullable: false),
                    arrival_city = table.Column<string>(type: "TEXT", nullable: false),
                    price_per_weight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    price_per_volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    min_payment = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freightfee", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "global_unique_serial",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    table_name = table.Column<string>(type: "TEXT", nullable: false),
                    prefix_char = table.Column<string>(type: "TEXT", nullable: false),
                    reset_rule = table.Column<string>(type: "TEXT", nullable: false),
                    current_no = table.Column<int>(type: "INTEGER", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_unique_serial", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "goodslocation",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    warehouse_id = table.Column<int>(type: "INTEGER", nullable: false),
                    warehouse_name = table.Column<string>(type: "TEXT", nullable: false),
                    warehouse_area_name = table.Column<string>(type: "TEXT", nullable: false),
                    warehouse_area_property = table.Column<byte>(type: "INTEGER", nullable: false),
                    location_name = table.Column<string>(type: "TEXT", nullable: false),
                    location_length = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    location_width = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    location_heigth = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    location_volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    location_load = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    roadway_number = table.Column<string>(type: "TEXT", nullable: false),
                    shelf_number = table.Column<string>(type: "TEXT", nullable: false),
                    layer_number = table.Column<string>(type: "TEXT", nullable: false),
                    tag_number = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    warehouse_area_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goodslocation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "goodsowner",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    goods_owner_name = table.Column<string>(type: "TEXT", nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: false),
                    manager = table.Column<string>(type: "TEXT", nullable: false),
                    contact_tel = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goodsowner", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    menu_name = table.Column<string>(type: "TEXT", nullable: false),
                    module = table.Column<string>(type: "TEXT", nullable: false),
                    vue_path = table.Column<string>(type: "TEXT", nullable: false),
                    vue_path_detail = table.Column<string>(type: "TEXT", nullable: false),
                    vue_directory = table.Column<string>(type: "TEXT", nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    menu_actions = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rolemenu",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    userrole_id = table.Column<int>(type: "INTEGER", nullable: false),
                    menu_id = table.Column<int>(type: "INTEGER", nullable: false),
                    authority = table.Column<byte>(type: "INTEGER", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    menu_actions_authority = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rolemenu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "spu",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    spu_code = table.Column<string>(type: "TEXT", nullable: false),
                    spu_name = table.Column<string>(type: "TEXT", nullable: false),
                    category_id = table.Column<int>(type: "INTEGER", nullable: false),
                    spu_description = table.Column<string>(type: "TEXT", nullable: false),
                    bar_code = table.Column<string>(type: "TEXT", nullable: false),
                    supplier_id = table.Column<int>(type: "INTEGER", nullable: false),
                    supplier_name = table.Column<string>(type: "TEXT", nullable: false),
                    brand = table.Column<string>(type: "TEXT", nullable: false),
                    origin = table.Column<string>(type: "TEXT", nullable: false),
                    length_unit = table.Column<byte>(type: "INTEGER", nullable: false),
                    volume_unit = table.Column<byte>(type: "INTEGER", nullable: false),
                    weight_unit = table.Column<byte>(type: "INTEGER", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    qty = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    is_freeze = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    putaway_date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stockadjust",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    job_code = table.Column<string>(type: "TEXT", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    qty = table.Column<int>(type: "INTEGER", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    is_update_stock = table.Column<bool>(type: "INTEGER", nullable: false),
                    job_type = table.Column<byte>(type: "INTEGER", nullable: false),
                    source_table_id = table.Column<int>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    putaway_date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stockadjust", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stockfreeze",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    job_code = table.Column<string>(type: "TEXT", nullable: false),
                    job_type = table.Column<bool>(type: "INTEGER", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    handler = table.Column<string>(type: "TEXT", nullable: false),
                    handle_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stockfreeze", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stockmove",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    job_code = table.Column<string>(type: "TEXT", nullable: false),
                    move_status = table.Column<byte>(type: "INTEGER", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    orig_goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    dest_googs_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    qty = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    handler = table.Column<string>(type: "TEXT", nullable: false),
                    handle_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    putaway_date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stockmove", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stockprocess",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    job_code = table.Column<string>(type: "TEXT", nullable: false),
                    job_type = table.Column<bool>(type: "INTEGER", nullable: false),
                    process_status = table.Column<bool>(type: "INTEGER", nullable: false),
                    processor = table.Column<string>(type: "TEXT", nullable: false),
                    process_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stockprocess", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stocktaking",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    job_code = table.Column<string>(type: "TEXT", nullable: false),
                    job_status = table.Column<bool>(type: "INTEGER", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    putaway_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    book_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    counted_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    difference_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    handler = table.Column<string>(type: "TEXT", nullable: false),
                    handle_time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktaking", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    supplier_name = table.Column<string>(type: "TEXT", nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    manager = table.Column<string>(type: "TEXT", nullable: false),
                    contact_tel = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_num = table.Column<string>(type: "TEXT", nullable: false),
                    user_name = table.Column<string>(type: "TEXT", nullable: false),
                    contact_tel = table.Column<string>(type: "TEXT", nullable: false),
                    user_role = table.Column<string>(type: "TEXT", nullable: false),
                    sex = table.Column<string>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    auth_string = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_defined_print_solution",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    vue_path = table.Column<string>(type: "TEXT", nullable: false),
                    tab_page = table.Column<string>(type: "TEXT", nullable: false),
                    solution_name = table.Column<string>(type: "TEXT", nullable: false),
                    config_json = table.Column<string>(type: "TEXT", nullable: false),
                    report_length = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    report_width = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    report_direction = table.Column<string>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_defined_print_solution", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "userrole",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_name = table.Column<string>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userrole", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "warehouse",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    warehouse_name = table.Column<string>(type: "TEXT", nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    manager = table.Column<string>(type: "TEXT", nullable: false),
                    contact_tel = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "warehousearea",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    warehouse_id = table.Column<int>(type: "INTEGER", nullable: false),
                    area_name = table.Column<string>(type: "TEXT", nullable: false),
                    parent_id = table.Column<int>(type: "INTEGER", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    area_property = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehousearea", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asn",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    asnmaster_id = table.Column<int>(type: "INTEGER", nullable: false),
                    asn_no = table.Column<string>(type: "TEXT", nullable: false),
                    asn_status = table.Column<byte>(type: "INTEGER", nullable: false),
                    spu_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    asn_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    actual_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    arrival_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    unload_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    unload_person_id = table.Column<int>(type: "INTEGER", nullable: false),
                    unload_person = table.Column<string>(type: "TEXT", nullable: false),
                    sorted_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    shortage_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    more_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    damage_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    weight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    supplier_id = table.Column<int>(type: "INTEGER", nullable: false),
                    supplier_name = table.Column<string>(type: "TEXT", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_name = table.Column<string>(type: "TEXT", nullable: false),
                    creator = table.Column<string>(type: "TEXT", nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asn", x => x.id);
                    table.ForeignKey(
                        name: "FK_asn_asnmaster_asnmaster_id",
                        column: x => x.asnmaster_id,
                        principalTable: "asnmaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispatchpicklist",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    dispatchlist_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    pick_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    picked_qty = table.Column<int>(type: "INTEGER", nullable: false),
                    is_update_stock = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    picker_id = table.Column<int>(type: "INTEGER", nullable: false),
                    picker = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    putaway_date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatchpicklist", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatchpicklist_dispatchlist_dispatchlist_id",
                        column: x => x.dispatchlist_id,
                        principalTable: "dispatchlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flowset",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    flowsetmain_id = table.Column<int>(type: "INTEGER", nullable: false),
                    is_origin = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_end = table.Column<bool>(type: "INTEGER", nullable: false),
                    node_guid = table.Column<string>(type: "TEXT", nullable: false),
                    node_name = table.Column<string>(type: "TEXT", nullable: false),
                    prev_node_guid = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flowset", x => x.id);
                    table.ForeignKey(
                        name: "FK_flowset_flowsetmain_flowsetmain_id",
                        column: x => x.flowsetmain_id,
                        principalTable: "flowsetmain",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sku",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    spu_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sku_code = table.Column<string>(type: "TEXT", nullable: false),
                    sku_name = table.Column<string>(type: "TEXT", nullable: false),
                    bar_code = table.Column<string>(type: "TEXT", nullable: false),
                    weight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    lenght = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    width = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    height = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false),
                    cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku", x => x.id);
                    table.ForeignKey(
                        name: "FK_sku_spu_spu_id",
                        column: x => x.spu_id,
                        principalTable: "spu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stockprocessdetail",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    stock_process_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_owner_id = table.Column<int>(type: "INTEGER", nullable: false),
                    goods_location_id = table.Column<int>(type: "INTEGER", nullable: false),
                    qty = table.Column<int>(type: "INTEGER", nullable: false),
                    last_update_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<long>(type: "INTEGER", nullable: false),
                    is_source = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_update_stock = table.Column<bool>(type: "INTEGER", nullable: false),
                    series_number = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    putaway_date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stockprocessdetail", x => x.id);
                    table.ForeignKey(
                        name: "FK_stockprocessdetail_stockprocess_stock_process_id",
                        column: x => x.stock_process_id,
                        principalTable: "stockprocess",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flowsetfilter",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    flowset_id = table.Column<int>(type: "INTEGER", nullable: false),
                    flowsetmain_id = table.Column<int>(type: "INTEGER", nullable: false),
                    node_guid = table.Column<string>(type: "TEXT", nullable: false),
                    logic = table.Column<string>(type: "TEXT", nullable: false),
                    c1 = table.Column<string>(type: "TEXT", nullable: false),
                    col_label = table.Column<string>(type: "TEXT", nullable: false),
                    col_name = table.Column<string>(type: "TEXT", nullable: false),
                    compare = table.Column<string>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    c2 = table.Column<string>(type: "TEXT", nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    condition_group = table.Column<string>(type: "TEXT", nullable: false),
                    formulas = table.Column<string>(type: "TEXT", nullable: false),
                    assert_mode = table.Column<string>(type: "TEXT", nullable: false),
                    table_name = table.Column<string>(type: "TEXT", nullable: false),
                    scheme_name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flowsetfilter", x => x.id);
                    table.ForeignKey(
                        name: "FK_flowsetfilter_flowset_flowset_id",
                        column: x => x.flowset_id,
                        principalTable: "flowset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flowsetusers",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    flowset_id = table.Column<int>(type: "INTEGER", nullable: false),
                    flowsetmain_id = table.Column<int>(type: "INTEGER", nullable: false),
                    node_guid = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flowsetusers", x => x.id);
                    table.ForeignKey(
                        name: "FK_flowsetusers_flowset_flowset_id",
                        column: x => x.flowset_id,
                        principalTable: "flowset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sku_safety_stock",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sku_id = table.Column<int>(type: "INTEGER", nullable: false),
                    warehouse_id = table.Column<int>(type: "INTEGER", nullable: false),
                    safety_stock_qty = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku_safety_stock", x => x.id);
                    table.ForeignKey(
                        name: "FK_sku_safety_stock_sku_sku_id",
                        column: x => x.sku_id,
                        principalTable: "sku",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asn_asnmaster_id",
                table: "asn",
                column: "asnmaster_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatchpicklist_dispatchlist_id",
                table: "dispatchpicklist",
                column: "dispatchlist_id");

            migrationBuilder.CreateIndex(
                name: "IX_flowset_flowsetmain_id",
                table: "flowset",
                column: "flowsetmain_id");

            migrationBuilder.CreateIndex(
                name: "IX_flowsetfilter_flowset_id",
                table: "flowsetfilter",
                column: "flowset_id");

            migrationBuilder.CreateIndex(
                name: "IX_flowsetusers_flowset_id",
                table: "flowsetusers",
                column: "flowset_id");

            migrationBuilder.CreateIndex(
                name: "IX_sku_spu_id",
                table: "sku",
                column: "spu_id");

            migrationBuilder.CreateIndex(
                name: "IX_sku_safety_stock_sku_id",
                table: "sku_safety_stock",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "IX_stockprocessdetail_stock_process_id",
                table: "stockprocessdetail",
                column: "stock_process_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_log");

            migrationBuilder.DropTable(
                name: "asn");

            migrationBuilder.DropTable(
                name: "asnsort");

            migrationBuilder.DropTable(
                name: "category");

            migrationBuilder.DropTable(
                name: "company");

            migrationBuilder.DropTable(
                name: "customer");

            migrationBuilder.DropTable(
                name: "dispatchpicklist");

            migrationBuilder.DropTable(
                name: "flowsetfilter");

            migrationBuilder.DropTable(
                name: "flowsetusers");

            migrationBuilder.DropTable(
                name: "freightfee");

            migrationBuilder.DropTable(
                name: "global_unique_serial");

            migrationBuilder.DropTable(
                name: "goodslocation");

            migrationBuilder.DropTable(
                name: "goodsowner");

            migrationBuilder.DropTable(
                name: "menu");

            migrationBuilder.DropTable(
                name: "rolemenu");

            migrationBuilder.DropTable(
                name: "sku_safety_stock");

            migrationBuilder.DropTable(
                name: "stock");

            migrationBuilder.DropTable(
                name: "stockadjust");

            migrationBuilder.DropTable(
                name: "stockfreeze");

            migrationBuilder.DropTable(
                name: "stockmove");

            migrationBuilder.DropTable(
                name: "stockprocessdetail");

            migrationBuilder.DropTable(
                name: "stocktaking");

            migrationBuilder.DropTable(
                name: "supplier");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "user_defined_print_solution");

            migrationBuilder.DropTable(
                name: "userrole");

            migrationBuilder.DropTable(
                name: "warehouse");

            migrationBuilder.DropTable(
                name: "warehousearea");

            migrationBuilder.DropTable(
                name: "asnmaster");

            migrationBuilder.DropTable(
                name: "dispatchlist");

            migrationBuilder.DropTable(
                name: "flowset");

            migrationBuilder.DropTable(
                name: "sku");

            migrationBuilder.DropTable(
                name: "stockprocess");

            migrationBuilder.DropTable(
                name: "flowsetmain");

            migrationBuilder.DropTable(
                name: "spu");
        }
    }
}
