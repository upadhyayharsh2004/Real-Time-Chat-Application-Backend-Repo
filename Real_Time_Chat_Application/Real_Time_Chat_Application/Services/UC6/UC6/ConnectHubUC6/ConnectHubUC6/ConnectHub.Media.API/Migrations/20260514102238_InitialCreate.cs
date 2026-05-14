using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConnectHub.Media.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "media");

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                schema: "media",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UploadedBy = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeKb = table.Column<long>(type: "bigint", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MessageId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.FileId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ContentType",
                schema: "media",
                table: "MediaFiles",
                column: "ContentType");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ExpiresAt",
                schema: "media",
                table: "MediaFiles",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_MessageId",
                schema: "media",
                table: "MediaFiles",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_RoomId",
                schema: "media",
                table: "MediaFiles",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_UploadedBy",
                schema: "media",
                table: "MediaFiles",
                column: "UploadedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaFiles",
                schema: "media");
        }
    }
}
