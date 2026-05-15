using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEdiDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdiDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    TransactionSetCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    InterchangeControlNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GroupControlNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransactionControlNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SenderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReceiverId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SenderQualifier = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ReceiverQualifier = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CanonicalType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanonicalJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseDocumentId = table.Column<int>(type: "int", nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "int", nullable: true),
                    AcknowledgmentGenerated = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgmentSent = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgmentSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawEdiContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LineItemCount = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProcessingErrors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdiDocuments_EdiDocuments_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "EdiDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EdiDocuments_EdiDocuments_ResponseDocumentId",
                        column: x => x.ResponseDocumentId,
                        principalTable: "EdiDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EdiDocuments_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_BusinessReference",
                table: "EdiDocuments",
                column: "BusinessReference");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_Direction",
                table: "EdiDocuments",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_InterchangeControlNumber",
                table: "EdiDocuments",
                column: "InterchangeControlNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_OriginalDocumentId",
                table: "EdiDocuments",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_PartnerDocumentId",
                table: "EdiDocuments",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_ResponseDocumentId",
                table: "EdiDocuments",
                column: "ResponseDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_SenderId_ReceiverId",
                table: "EdiDocuments",
                columns: new[] { "SenderId", "ReceiverId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_TransactionSetCode",
                table: "EdiDocuments",
                column: "TransactionSetCode");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_TransactionSetCode_Direction",
                table: "EdiDocuments",
                columns: new[] { "TransactionSetCode", "Direction" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdiDocuments");
        }
    }
}
