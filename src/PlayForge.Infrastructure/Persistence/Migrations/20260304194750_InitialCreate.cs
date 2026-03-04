using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    platform = table.Column<int>(type: "integer", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    screenshots = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    release_date = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    review_summary = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    review_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    steam_id = table.Column<long>(type: "bigint", nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    invite_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    host_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_groups_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_games",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    playtime_minutes = table.Column<int>(type: "integer", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_games", x => new { x.UserId, x.GameId });
                    table.ForeignKey(
                        name: "FK_user_games_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_games_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_candidates_game_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "game_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_memberships",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_group_memberships_game_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "game_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    candidate_game_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    winner_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vote_sessions_game_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "game_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votes",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cast_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votes", x => new { x.SessionId, x.UserId, x.game_id });
                    table.ForeignKey(
                        name: "FK_votes_vote_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "vote_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_groups_invite_code",
                table: "game_groups",
                column: "invite_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_groups_UserId",
                table: "game_groups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_games_app_id",
                table: "games",
                column: "app_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_candidates_group_id",
                table: "group_candidates",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_UserId",
                table: "group_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_games_GameId",
                table: "user_games",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_users_steam_id",
                table: "users",
                column: "steam_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vote_sessions_group_id",
                table: "vote_sessions",
                column: "group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_candidates");

            migrationBuilder.DropTable(
                name: "group_memberships");

            migrationBuilder.DropTable(
                name: "user_games");

            migrationBuilder.DropTable(
                name: "votes");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "vote_sessions");

            migrationBuilder.DropTable(
                name: "game_groups");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
