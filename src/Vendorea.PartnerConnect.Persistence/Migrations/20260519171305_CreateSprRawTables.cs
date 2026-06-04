using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateSprRawTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spr");

            // Drop existing tables created by AddSprRawSchema migration (if they exist)
            // These tables were created with composite PKs and native types, but we need
            // surrogate identity PKs and nvarchar columns for the XML import process
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[search_attribute]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[search_attribute_values]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productattribute]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productaccessories]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productsimilar]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productupsell]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productfeatures]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productdescriptions]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productimages]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productkeywords]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productlocales]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productresources]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[productskus]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[product]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[categorydisplayattributes]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[categoryheader]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[categorysearchattributes]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[categorynames]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[category]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[attributenames]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[headernames]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[unitnames]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[units]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[locales]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[manufacturer]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[mapped_category]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[mapped_category_names]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[mapped_category_taxonomy]");

            migrationBuilder.CreateTable(
                name: "attributenames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    attributeid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attributenames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "category",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    categoryid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    parentcategoryid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ordernumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    catlevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    displayorder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lastupdated = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorydisplayattributes",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    headerid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    attributeid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    templatetype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    defaultdisplayorder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    displayorder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lastupdated = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorydisplayattributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categoryheader",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    headerid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    templatetype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    defaultdisplayorder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    displayorder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lastupdated = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categoryheader", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorynames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorynames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorysearchattributes",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    attributeid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ispreferred = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lastupdated = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorysearchattributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "headernames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    headerid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_headernames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locales",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    languagecode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    countrycode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturer",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    manufacturerid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    city = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    zip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    state = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lastupdated = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mapped_category",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapped_category", x => x.productid);
                });

            migrationBuilder.CreateTable(
                name: "mapped_category_names",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    localeid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapped_category_names", x => new { x.categoryid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "mapped_category_taxonomy",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    parentcategoryid = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapped_category_taxonomy", x => x.categoryid);
                });

            migrationBuilder.CreateTable(
                name: "product",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    manufacturerid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    mfgpartno = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    categoryid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isaccessory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    equivalency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    creationdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    modifieddate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lastupdated = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productaccessories",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    accessoryproductid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ispreferred = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isoption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    recommendation_weight = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productaccessories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productattribute",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<int>(type: "int", nullable: false),
                    attributeid = table.Column<long>(type: "bigint", nullable: false),
                    categoryid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    displayvalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    absolutevalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    unitid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isabsolute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productattribute", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productdescriptions",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isdefault = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productdescriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productfeatures",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sequenceno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bullettext = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productfeatures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productimages",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productimages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productkeywords",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    keywords = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productkeywords", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productlocales",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productlocales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productresources",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    skuname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sku = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    startdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    enddate = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productresources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productsimilar",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    similarproductid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productsimilar", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productskus",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    sku = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    addeddate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    discontinueddate = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productskus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productupsell",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    upsellproductid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productupsell", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_attribute",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    attributeid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    localeid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    valueid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    absolutevalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isabsolute = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_attribute", x => new { x.productid, x.attributeid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "search_attribute_values",
                schema: "spr",
                columns: table => new
                {
                    valueid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    absolutevalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    unitid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isabsolute = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_attribute_values", x => x.valueid);
                });

            migrationBuilder.CreateTable(
                name: "unitnames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    unitid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unitnames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    unitid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    baseunitid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    multiple = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attributenames_attributeid",
                schema: "spr",
                table: "attributenames",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_category_parentcategoryid",
                schema: "spr",
                table: "category",
                column: "parentcategoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categorydisplayattributes_attributeid",
                schema: "spr",
                table: "categorydisplayattributes",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_categorydisplayattributes_categoryid",
                schema: "spr",
                table: "categorydisplayattributes",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categoryheader_categoryid",
                schema: "spr",
                table: "categoryheader",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categoryheader_headerid",
                schema: "spr",
                table: "categoryheader",
                column: "headerid");

            migrationBuilder.CreateIndex(
                name: "IX_categorynames_categoryid",
                schema: "spr",
                table: "categorynames",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categorysearchattributes_attributeid",
                schema: "spr",
                table: "categorysearchattributes",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_categorysearchattributes_categoryid",
                schema: "spr",
                table: "categorysearchattributes",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_headernames_headerid",
                schema: "spr",
                table: "headernames",
                column: "headerid");

            migrationBuilder.CreateIndex(
                name: "IX_mapped_category_categoryid",
                schema: "spr",
                table: "mapped_category",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_mapped_category_names_categoryid",
                schema: "spr",
                table: "mapped_category_names",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_mapped_category_taxonomy_parentcategoryid",
                schema: "spr",
                table: "mapped_category_taxonomy",
                column: "parentcategoryid");

            migrationBuilder.CreateIndex(
                name: "IX_product_categoryid",
                schema: "spr",
                table: "product",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_product_manufacturerid",
                schema: "spr",
                table: "product",
                column: "manufacturerid");

            migrationBuilder.CreateIndex(
                name: "IX_product_mfgpartno",
                schema: "spr",
                table: "product",
                column: "mfgpartno");

            migrationBuilder.CreateIndex(
                name: "IX_productaccessories_accessoryproductid",
                schema: "spr",
                table: "productaccessories",
                column: "accessoryproductid");

            migrationBuilder.CreateIndex(
                name: "IX_productaccessories_productid",
                schema: "spr",
                table: "productaccessories",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productattribute_attributeid",
                schema: "spr",
                table: "productattribute",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_productattribute_productid",
                schema: "spr",
                table: "productattribute",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productdescriptions_productid",
                schema: "spr",
                table: "productdescriptions",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productfeatures_productid",
                schema: "spr",
                table: "productfeatures",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productimages_productid",
                schema: "spr",
                table: "productimages",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productkeywords_productid",
                schema: "spr",
                table: "productkeywords",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productlocales_productid",
                schema: "spr",
                table: "productlocales",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productresources_productid",
                schema: "spr",
                table: "productresources",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productsimilar_productid",
                schema: "spr",
                table: "productsimilar",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productsimilar_similarproductid",
                schema: "spr",
                table: "productsimilar",
                column: "similarproductid");

            migrationBuilder.CreateIndex(
                name: "IX_productskus_name",
                schema: "spr",
                table: "productskus",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_productskus_productid",
                schema: "spr",
                table: "productskus",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productskus_sku",
                schema: "spr",
                table: "productskus",
                column: "sku");

            migrationBuilder.CreateIndex(
                name: "IX_productupsell_productid",
                schema: "spr",
                table: "productupsell",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productupsell_upsellproductid",
                schema: "spr",
                table: "productupsell",
                column: "upsellproductid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_attributeid",
                schema: "spr",
                table: "search_attribute",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_productid",
                schema: "spr",
                table: "search_attribute",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_valueid",
                schema: "spr",
                table: "search_attribute",
                column: "valueid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_values_value",
                schema: "spr",
                table: "search_attribute_values",
                column: "value");

            migrationBuilder.CreateIndex(
                name: "IX_unitnames_unitid",
                schema: "spr",
                table: "unitnames",
                column: "unitid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attributenames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "category",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categorydisplayattributes",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categoryheader",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categorynames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categorysearchattributes",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "headernames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "locales",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "manufacturer",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "mapped_category",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "mapped_category_names",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "mapped_category_taxonomy",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "product",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productaccessories",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productattribute",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productdescriptions",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productfeatures",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productimages",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productkeywords",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productlocales",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productresources",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productsimilar",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productskus",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productupsell",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "search_attribute",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "search_attribute_values",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "unitnames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "units",
                schema: "spr");
        }
    }
}
