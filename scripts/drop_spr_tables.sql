-- Drop all tables in spr schema
IF OBJECT_ID('spr.productaccessories', 'U') IS NOT NULL DROP TABLE spr.productaccessories;
IF OBJECT_ID('spr.productsimilar', 'U') IS NOT NULL DROP TABLE spr.productsimilar;
IF OBJECT_ID('spr.productupsell', 'U') IS NOT NULL DROP TABLE spr.productupsell;
IF OBJECT_ID('spr.productfeatures', 'U') IS NOT NULL DROP TABLE spr.productfeatures;
IF OBJECT_ID('spr.productresources', 'U') IS NOT NULL DROP TABLE spr.productresources;
IF OBJECT_ID('spr.productskus', 'U') IS NOT NULL DROP TABLE spr.productskus;
IF OBJECT_ID('spr.productlocales', 'U') IS NOT NULL DROP TABLE spr.productlocales;
IF OBJECT_ID('spr.productkeywords', 'U') IS NOT NULL DROP TABLE spr.productkeywords;
IF OBJECT_ID('spr.productimages', 'U') IS NOT NULL DROP TABLE spr.productimages;
IF OBJECT_ID('spr.productdescriptions', 'U') IS NOT NULL DROP TABLE spr.productdescriptions;
IF OBJECT_ID('spr.productattribute', 'U') IS NOT NULL DROP TABLE spr.productattribute;
IF OBJECT_ID('spr.search_attribute', 'U') IS NOT NULL DROP TABLE spr.search_attribute;
IF OBJECT_ID('spr.search_attribute_values', 'U') IS NOT NULL DROP TABLE spr.search_attribute_values;
IF OBJECT_ID('spr.product', 'U') IS NOT NULL DROP TABLE spr.product;
IF OBJECT_ID('spr.categorysearchattributes', 'U') IS NOT NULL DROP TABLE spr.categorysearchattributes;
IF OBJECT_ID('spr.categorydisplayattributes', 'U') IS NOT NULL DROP TABLE spr.categorydisplayattributes;
IF OBJECT_ID('spr.categoryheader', 'U') IS NOT NULL DROP TABLE spr.categoryheader;
IF OBJECT_ID('spr.categorynames', 'U') IS NOT NULL DROP TABLE spr.categorynames;
IF OBJECT_ID('spr.category', 'U') IS NOT NULL DROP TABLE spr.category;
IF OBJECT_ID('spr.attributenames', 'U') IS NOT NULL DROP TABLE spr.attributenames;
IF OBJECT_ID('spr.headernames', 'U') IS NOT NULL DROP TABLE spr.headernames;
IF OBJECT_ID('spr.unitnames', 'U') IS NOT NULL DROP TABLE spr.unitnames;
IF OBJECT_ID('spr.units', 'U') IS NOT NULL DROP TABLE spr.units;
IF OBJECT_ID('spr.locales', 'U') IS NOT NULL DROP TABLE spr.locales;
IF OBJECT_ID('spr.manufacturer', 'U') IS NOT NULL DROP TABLE spr.manufacturer;
IF OBJECT_ID('spr.mapped_category', 'U') IS NOT NULL DROP TABLE spr.mapped_category;
IF OBJECT_ID('spr.mapped_category_names', 'U') IS NOT NULL DROP TABLE spr.mapped_category_names;
IF OBJECT_ID('spr.mapped_category_taxonomy', 'U') IS NOT NULL DROP TABLE spr.mapped_category_taxonomy;

-- Clean up migration history for SPR-related migrations
DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%UpdateProductFeaturesKey%';
DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%UpdateAttributeAndAccessoryKeys%';
DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%ChangeAbsoluteValueToString%';
DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%MakeAttributeColumnsStrings%';
DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%MakeAccessoryColumnsStrings%';

PRINT 'All SPR tables dropped successfully';
