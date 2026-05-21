using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprRawSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spr");

            migrationBuilder.CreateTable(
                name: "attributenames",
                schema: "spr",
                columns: table => new
                {
                    attributeid = table.Column<long>(type: "bigint", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(110)", maxLength: 110, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attributenames", x => new { x.attributeid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "category",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    parentcategoryid = table.Column<int>(type: "int", nullable: true),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    ordernumber = table.Column<int>(type: "int", nullable: true),
                    catlevel = table.Column<int>(type: "int", nullable: true),
                    displayorder = table.Column<int>(type: "int", nullable: true),
                    lastupdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.categoryid);
                });

            migrationBuilder.CreateTable(
                name: "categorydisplayattributes",
                schema: "spr",
                columns: table => new
                {
                    headerid = table.Column<int>(type: "int", nullable: false),
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    attributeid = table.Column<long>(type: "bigint", nullable: false),
                    templatetype = table.Column<int>(type: "int", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    defaultdisplayorder = table.Column<int>(type: "int", nullable: true),
                    displayorder = table.Column<int>(type: "int", nullable: true),
                    lastupdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorydisplayattributes", x => new { x.categoryid, x.headerid, x.attributeid, x.templatetype });
                });

            migrationBuilder.CreateTable(
                name: "categoryheader",
                schema: "spr",
                columns: table => new
                {
                    headerid = table.Column<int>(type: "int", nullable: false),
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    templatetype = table.Column<int>(type: "int", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    defaultdisplayorder = table.Column<int>(type: "int", nullable: true),
                    displayorder = table.Column<int>(type: "int", nullable: true),
                    lastupdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categoryheader", x => new { x.categoryid, x.headerid, x.templatetype });
                });

            migrationBuilder.CreateTable(
                name: "categorynames",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorynames", x => new { x.categoryid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "categorysearchattributes",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    attributeid = table.Column<long>(type: "bigint", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    ispreferred = table.Column<bool>(type: "bit", nullable: false),
                    lastupdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorysearchattributes", x => new { x.categoryid, x.attributeid });
                });

            migrationBuilder.CreateTable(
                name: "headernames",
                schema: "spr",
                columns: table => new
                {
                    headerid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_headernames", x => new { x.headerid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "locales",
                schema: "spr",
                columns: table => new
                {
                    localeid = table.Column<int>(type: "int", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    languagecode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    countrycode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locales", x => x.localeid);
                });

            migrationBuilder.CreateTable(
                name: "manufacturer",
                schema: "spr",
                columns: table => new
                {
                    manufacturerid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    address1 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    address2 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    city = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    url = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    fax = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    country = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    state = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    lastupdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturer", x => x.manufacturerid);
                });

            migrationBuilder.CreateTable(
                name: "mapped_category",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    categoryid = table.Column<int>(type: "int", nullable: false)
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
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
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
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    parentcategoryid = table.Column<int>(type: "int", nullable: true)
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
                    productid = table.Column<int>(type: "int", nullable: false),
                    manufacturerid = table.Column<int>(type: "int", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    mfgpartno = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    isaccessory = table.Column<bool>(type: "bit", nullable: false),
                    equivalency = table.Column<double>(type: "float", nullable: true),
                    creationdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modifieddate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    lastupdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.productid);
                });

            migrationBuilder.CreateTable(
                name: "productaccessories",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    accessoryproductid = table.Column<int>(type: "int", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    ispreferred = table.Column<bool>(type: "bit", nullable: false),
                    isoption = table.Column<bool>(type: "bit", nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    recommendation_weight = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productaccessories", x => new { x.productid, x.accessoryproductid });
                });

            migrationBuilder.CreateTable(
                name: "productattribute",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    attributeid = table.Column<long>(type: "bigint", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    categoryid = table.Column<int>(type: "int", nullable: false),
                    displayvalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    absolutevalue = table.Column<double>(type: "float", nullable: true),
                    unitid = table.Column<int>(type: "int", nullable: true),
                    isabsolute = table.Column<bool>(type: "bit", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productattribute", x => new { x.productid, x.attributeid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productdescriptions",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isdefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productdescriptions", x => new { x.productid, x.type, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productfeatures",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    sequenceno = table.Column<int>(type: "int", nullable: false),
                    bullettext = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productfeatures", x => new { x.productid, x.localeid, x.sequenceno });
                });

            migrationBuilder.CreateTable(
                name: "productimages",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productimages", x => new { x.productid, x.type });
                });

            migrationBuilder.CreateTable(
                name: "productkeywords",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    keywords = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productkeywords", x => new { x.productid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productlocales",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    isactive = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productlocales", x => new { x.productid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productresources",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    skuname = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    startdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    enddate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productresources", x => new { x.productid, x.type, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productsimilar",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    similarproductid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productsimilar", x => new { x.productid, x.similarproductid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productskus",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    addeddate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    discontinueddate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productskus", x => new { x.productid, x.name, x.sku, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "productupsell",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    upsellproductid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productupsell", x => new { x.productid, x.upsellproductid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "search_attribute",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<int>(type: "int", nullable: false),
                    attributeid = table.Column<long>(type: "bigint", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    valueid = table.Column<int>(type: "int", nullable: false),
                    absolutevalue = table.Column<double>(type: "float", nullable: true),
                    isabsolute = table.Column<bool>(type: "bit", nullable: false)
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
                    valueid = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    absolutevalue = table.Column<double>(type: "float", nullable: true),
                    unitid = table.Column<int>(type: "int", nullable: true),
                    isabsolute = table.Column<bool>(type: "bit", nullable: false)
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
                    unitid = table.Column<int>(type: "int", nullable: false),
                    localeid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unitnames", x => new { x.unitid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "units",
                schema: "spr",
                columns: table => new
                {
                    unitid = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    baseunitid = table.Column<int>(type: "int", nullable: true),
                    multiple = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.unitid);
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
