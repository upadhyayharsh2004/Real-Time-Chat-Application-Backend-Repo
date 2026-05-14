using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConnectHub.ChatRoom.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chatroom");

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                schema: "chatroom",
                columns: table => new
                {
                    RoomId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RoomType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "PUBLIC"),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.RoomId);
                });

            migrationBuilder.CreateTable(
                name: "RoomInvites",
                schema: "chatroom",
                columns: table => new
                {
                    InviteId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<int>(type: "integer", nullable: false),
                    InvitedUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomInvites", x => x.InviteId);
                    table.ForeignKey(
                        name: "FK_RoomInvites_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "chatroom",
                        principalTable: "ChatRooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomMembers",
                schema: "chatroom",
                columns: table => new
                {
                    RoomMemberId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "MEMBER"),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMembers", x => x.RoomMemberId);
                    table.ForeignKey(
                        name: "FK_RoomMembers_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "chatroom",
                        principalTable: "ChatRooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatedByUserId",
                schema: "chatroom",
                table: "ChatRooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_DeletedAt",
                schema: "chatroom",
                table: "ChatRooms",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_RoomType_IsActive",
                schema: "chatroom",
                table: "ChatRooms",
                columns: new[] { "RoomType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomInvites_InvitedUserId_Status",
                schema: "chatroom",
                table: "RoomInvites",
                columns: new[] { "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomInvites_RoomId_UserId_Status",
                schema: "chatroom",
                table: "RoomInvites",
                columns: new[] { "RoomId", "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomMembers_RoomId_Role",
                schema: "chatroom",
                table: "RoomMembers",
                columns: new[] { "RoomId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomMembers_RoomId_UserId",
                schema: "chatroom",
                table: "RoomMembers",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomMembers_UserId_IsActive",
                schema: "chatroom",
                table: "RoomMembers",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomInvites",
                schema: "chatroom");

            migrationBuilder.DropTable(
                name: "RoomMembers",
                schema: "chatroom");

            migrationBuilder.DropTable(
                name: "ChatRooms",
                schema: "chatroom");
        }
    }
}
