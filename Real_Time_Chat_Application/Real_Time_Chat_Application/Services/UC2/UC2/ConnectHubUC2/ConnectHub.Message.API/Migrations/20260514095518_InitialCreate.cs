using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConnectHub.Message.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "message");

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "message",
                columns: table => new
                {
                    MessageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    ReceiverId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "TEXT"),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MediaUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplyToMessageId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ReplyToMessageId",
                        column: x => x.ReplyToMessageId,
                        principalSchema: "message",
                        principalTable: "Messages",
                        principalColumn: "MessageId");
                });

            migrationBuilder.CreateTable(
                name: "ConversationPins",
                schema: "message",
                columns: table => new
                {
                    PinId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    PinnedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ConversationWithUserId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    PinnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPins", x => x.PinId);
                    table.ForeignKey(
                        name: "FK_ConversationPins_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "message",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageReactions",
                schema: "message",
                columns: table => new
                {
                    ReactionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReactions", x => x.ReactionId);
                    table.ForeignKey(
                        name: "FK_MessageReactions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "message",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPins_MessageId_UserId",
                schema: "message",
                table: "ConversationPins",
                columns: new[] { "MessageId", "PinnedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_MessageId_UserId_Emoji",
                schema: "message",
                table: "MessageReactions",
                columns: new[] { "MessageId", "UserId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsDeleted",
                schema: "message",
                table: "Messages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReceiverId_IsRead",
                schema: "message",
                table: "Messages",
                columns: new[] { "ReceiverId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                schema: "message",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId_SentAt",
                schema: "message",
                table: "Messages",
                columns: new[] { "RoomId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_ReceiverId",
                schema: "message",
                table: "Messages",
                columns: new[] { "SenderId", "ReceiverId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationPins",
                schema: "message");

            migrationBuilder.DropTable(
                name: "MessageReactions",
                schema: "message");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "message");
        }
    }
}
