using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Slp.Common.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlpAddress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpAddress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlpBlock",
                columns: table => new
                {
                    Height = table.Column<int>(type: "int", nullable: false),
                    Hash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    BlockTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSlp = table.Column<byte>(type: "tinyint", nullable: false),
                    Orphaned = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpBlock", x => x.Height);
                });

            migrationBuilder.CreateTable(
                name: "SlpDatabaseState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastStatusUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockTip = table.Column<int>(type: "int", nullable: false),
                    BlockTipHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpDatabaseState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlpToken",
                columns: table => new
                {
                    Hash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    VersionType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentUri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentSha256Hex = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Decimals = table.Column<int>(type: "int", nullable: false),
                    LastActiveSend = table.Column<int>(type: "int", nullable: true),
                    ActiveMint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotalMinted = table.Column<decimal>(type: "decimal (38,0)", nullable: true),
                    TotalBurned = table.Column<decimal>(type: "decimal (38,0)", nullable: true),
                    CirculatingSupply = table.Column<decimal>(type: "decimal (38,0)", nullable: true),
                    ValidTokenUtxos = table.Column<int>(type: "int", nullable: true),
                    ValidAddresses = table.Column<int>(type: "int", nullable: true),
                    SatoshisLockedUp = table.Column<int>(type: "int", nullable: true),
                    TxnsSinceGenesis = table.Column<int>(type: "int", nullable: true),
                    MintingBatonStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BlockLastActiveSend = table.Column<int>(type: "int", nullable: true),
                    BlockLastActiveMint = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpToken", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "SlpTransaction",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    SlpTokenId = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    SlpTokenType = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    BlockHeight = table.Column<int>(type: "int", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    InvalidReason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MintBatonVOut = table.Column<int>(type: "int", nullable: true),
                    AdditionalTokenQuantity = table.Column<decimal>(type: "decimal (38,0)", nullable: true),
                    TokenInputSum = table.Column<decimal>(type: "decimal (38,0)", nullable: true),
                    TokenOutputSum = table.Column<decimal>(type: "decimal (38,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlpTransaction_SlpBlock_BlockHeight",
                        column: x => x.BlockHeight,
                        principalTable: "SlpBlock",
                        principalColumn: "Height",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SlpTransaction_SlpToken_SlpTokenId",
                        column: x => x.SlpTokenId,
                        principalTable: "SlpToken",
                        principalColumn: "Hash",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SlpTransactionInput",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SlpTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    AddressId = table.Column<int>(type: "int", nullable: false),
                    SlpAmount = table.Column<decimal>(type: "decimal (38,0)", nullable: false),
                    BlockchainSatoshis = table.Column<decimal>(type: "decimal (38,0)", nullable: false),
                    SourceTxHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    VOut = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpTransactionInput", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlpTransactionInput_SlpAddress_AddressId",
                        column: x => x.AddressId,
                        principalTable: "SlpAddress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlpTransactionInput_SlpTransaction_SlpTransactionId",
                        column: x => x.SlpTransactionId,
                        principalTable: "SlpTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlpTransactionOutput",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AddressId = table.Column<int>(type: "int", maxLength: 128, nullable: false),
                    VOut = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal (38,0)", nullable: false),
                    BlockchainSatoshis = table.Column<decimal>(type: "decimal (38,0)", nullable: false),
                    NextInputId = table.Column<long>(type: "bigint", nullable: true),
                    SlpTransactionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlpTransactionOutput", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlpTransactionOutput_SlpAddress_AddressId",
                        column: x => x.AddressId,
                        principalTable: "SlpAddress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlpTransactionOutput_SlpTransaction_SlpTransactionId",
                        column: x => x.SlpTransactionId,
                        principalTable: "SlpTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlpTransactionOutput_SlpTransactionInput_NextInputId",
                        column: x => x.NextInputId,
                        principalTable: "SlpTransactionInput",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlpAddress_Address",
                table: "SlpAddress",
                column: "Address",
                unique: true,
                filter: "[Address] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SlpBlock_Hash",
                table: "SlpBlock",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransaction_BlockHeight",
                table: "SlpTransaction",
                column: "BlockHeight");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransaction_Hash",
                table: "SlpTransaction",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransaction_SlpTokenId",
                table: "SlpTransaction",
                column: "SlpTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransactionInput_AddressId",
                table: "SlpTransactionInput",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransactionInput_SlpTransactionId",
                table: "SlpTransactionInput",
                column: "SlpTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransactionOutput_AddressId",
                table: "SlpTransactionOutput",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransactionOutput_NextInputId",
                table: "SlpTransactionOutput",
                column: "NextInputId");

            migrationBuilder.CreateIndex(
                name: "IX_SlpTransactionOutput_SlpTransactionId",
                table: "SlpTransactionOutput",
                column: "SlpTransactionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlpDatabaseState");

            migrationBuilder.DropTable(
                name: "SlpTransactionOutput");

            migrationBuilder.DropTable(
                name: "SlpTransactionInput");

            migrationBuilder.DropTable(
                name: "SlpAddress");

            migrationBuilder.DropTable(
                name: "SlpTransaction");

            migrationBuilder.DropTable(
                name: "SlpBlock");

            migrationBuilder.DropTable(
                name: "SlpToken");
        }
    }
}
