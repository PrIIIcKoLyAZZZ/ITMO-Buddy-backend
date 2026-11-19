using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace ITMO.Buddy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "faculties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculties", x => x.id);
                    table.CheckConstraint("ck_faculties_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_faculties_normalized_name_not_blank", "length(btrim(normalized_name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "interests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interests", x => x.id);
                    table.CheckConstraint("ck_interests_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_interests_normalized_name_not_blank", "length(btrim(normalized_name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.id);
                    table.CheckConstraint("ck_skills_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_skills_normalized_name_not_blank", "length(btrim(normalized_name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_activity_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    personal_data_consent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_activity_date", "last_activity_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_users_consent_date", "personal_data_consent_at_utc <= created_at_utc");
                    table.CheckConstraint("ck_users_email_not_blank", "length(btrim(email::text)) > 0");
                    table.CheckConstraint("ck_users_password_hash_not_blank", "length(btrim(password_hash)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    second_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pair_max_user_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "GREATEST(first_user_id, second_user_id)", stored: true),
                    pair_min_user_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "LEAST(first_user_id, second_user_id)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.CheckConstraint("ck_conversations_different_users", "first_user_id <> second_user_id");
                    table.CheckConstraint("ck_conversations_source_type", "source_type IN ('Match', 'ListingResponse')");
                    table.ForeignKey(
                        name: "fk_conversations_first_user",
                        column: x => x.first_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversations_second_user",
                        column: x => x.second_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    required_participants_count = table.Column<int>(type: "integer", nullable: false),
                    accepted_participants_count = table.Column<int>(type: "integer", nullable: false),
                    format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    event_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian'::regconfig, coalesce(title, '') || ' ' || coalesce(description, ''))", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listings", x => x.id);
                    table.CheckConstraint("ck_listings_category", "category IN ('Studies', 'Projects', 'Events', 'Interests')");
                    table.CheckConstraint("ck_listings_date_order", "deadline_utc IS NULL OR event_at_utc IS NULL OR deadline_utc <= event_at_utc");
                    table.CheckConstraint("ck_listings_deadline", "deadline_utc IS NULL OR deadline_utc > created_at_utc");
                    table.CheckConstraint("ck_listings_event_date", "event_at_utc IS NULL OR event_at_utc > created_at_utc");
                    table.CheckConstraint("ck_listings_format", "format IN ('Offline', 'Online')");
                    table.CheckConstraint("ck_listings_participants", "required_participants_count BETWEEN 1 AND 10 AND accepted_participants_count >= 0 AND accepted_participants_count <= required_participants_count");
                    table.CheckConstraint("ck_listings_status", "status IN ('Open', 'Closed')");
                    table.CheckConstraint("ck_listings_timestamps", "updated_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "fk_listings_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_read_date", "read_at_utc IS NULL OR read_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_notifications_type", "type IN ('MatchCreated', 'ListingResponseCreated', 'ListingResponseAccepted', 'ListingResponseRejected', 'MessageReceived')");
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_refresh_tokens_expiration", "expires_at_utc > created_at_utc");
                    table.CheckConstraint("ck_refresh_tokens_hash_not_blank", "length(btrim(token_hash)) > 0");
                    table.CheckConstraint("ck_refresh_tokens_revocation", "revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_blocks", x => x.id);
                    table.CheckConstraint("ck_user_blocks_different_users", "blocker_id <> blocked_user_id");
                    table.ForeignKey(
                        name: "fk_user_blocks_blocked_user",
                        column: x => x.blocked_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_blocks_blocker",
                        column: x => x.blocker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_likes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_likes", x => x.id);
                    table.CheckConstraint("ck_user_likes_different_users", "sender_id <> target_id");
                    table.ForeignKey(
                        name: "fk_user_likes_sender",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_likes_target",
                        column: x => x.target_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    second_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pair_max_user_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "GREATEST(first_user_id, second_user_id)", stored: true),
                    pair_min_user_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "LEAST(first_user_id, second_user_id)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_matches", x => x.id);
                    table.CheckConstraint("ck_user_matches_different_users", "first_user_id <> second_user_id");
                    table.ForeignKey(
                        name: "fk_user_matches_first_user",
                        column: x => x.first_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_matches_second_user",
                        column: x => x.second_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    photo_object_key = table.Column<string>(type: "text", nullable: true),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    course = table.Column<int>(type: "integer", nullable: true),
                    about_me = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    contact = table.Column<string>(type: "text", nullable: true),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian'::regconfig, coalesce(first_name, '') || ' ' || coalesce(last_name, '') || ' ' || coalesce(about_me, ''))", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.id);
                    table.CheckConstraint("ck_user_profiles_course", "course IS NULL OR course > 0");
                    table.ForeignKey(
                        name: "fk_user_profiles_faculties_faculty_id",
                        column: x => x.faculty_id,
                        principalTable: "faculties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.CheckConstraint("ck_messages_read_date", "read_at_utc IS NULL OR read_at_utc >= sent_at_utc");
                    table.CheckConstraint("ck_messages_text_not_blank", "length(btrim(text)) > 0");
                    table.ForeignKey(
                        name: "fk_messages_conversations",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messages_users_sender",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "listing_required_skills",
                columns: table => new
                {
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_required_skills", x => new { x.listing_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_listing_required_skills_listings",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_listing_required_skills_skills",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "listing_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_responses", x => x.id);
                    table.CheckConstraint("ck_listing_responses_decision_date", "decided_at_utc IS NULL OR decided_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_listing_responses_status", "status IN ('Pending', 'Accepted', 'Rejected')");
                    table.CheckConstraint("ck_listing_responses_status_decision", "(status = 'Pending' AND decided_at_utc IS NULL) OR (status IN ('Accepted', 'Rejected') AND decided_at_utc IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_listing_responses_listings",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_listing_responses_users_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_goals",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_goals", x => new { x.profile_id, x.goal_category });
                    table.CheckConstraint("ck_profile_goals_category", "goal_category IN ('Studies', 'Projects', 'Events', 'Interests')");
                    table.ForeignKey(
                        name: "fk_profile_goals_profiles",
                        column: x => x.profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_interests",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interest_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_interests", x => new { x.profile_id, x.interest_id });
                    table.ForeignKey(
                        name: "fk_profile_interests_interests",
                        column: x => x.interest_id,
                        principalTable: "interests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_profile_interests_profiles",
                        column: x => x.profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_skills",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_skills", x => new { x.profile_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_profile_skills_profiles",
                        column: x => x.profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_profile_skills_skills",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_first_user_created_at",
                table: "conversations",
                columns: new[] { "first_user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_second_user_created_at",
                table: "conversations",
                columns: new[] { "second_user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_source",
                table: "conversations",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ux_conversations_unordered_pair",
                table: "conversations",
                columns: new[] { "pair_min_user_id", "pair_max_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_faculties_normalized_name",
                table: "faculties",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_interests_name_trgm",
                table: "interests",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ux_interests_normalized_name",
                table: "interests",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_required_skills_skill_id",
                table: "listing_required_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_responses_candidate_created_at",
                table: "listing_responses",
                columns: new[] { "candidate_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_listing_responses_listing_status",
                table: "listing_responses",
                columns: new[] { "listing_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_listing_responses_listing_candidate",
                table: "listing_responses",
                columns: new[] { "listing_id", "candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listings_author_id",
                table: "listings",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_listings_category_status",
                table: "listings",
                columns: new[] { "category", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_description_trgm",
                table: "listings",
                column: "description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_search_vector",
                table: "listings",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_created_at",
                table: "listings",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_deadline",
                table: "listings",
                columns: new[] { "status", "deadline_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_title_trgm",
                table: "listings",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_sent_id",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_unread_conversation_sender",
                table: "messages",
                columns: new[] { "conversation_id", "sender_id" },
                filter: "read_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_created_id",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_unread_recipient",
                table: "notifications",
                column: "recipient_id",
                filter: "read_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_profile_goals_category_profile",
                table: "profile_goals",
                columns: new[] { "goal_category", "profile_id" });

            migrationBuilder.CreateIndex(
                name: "ix_profile_interests_interest_id",
                table: "profile_interests",
                column: "interest_id");

            migrationBuilder.CreateIndex(
                name: "ix_profile_skills_skill_id",
                table: "profile_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at_utc",
                table: "refresh_tokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_name_trgm",
                table: "skills",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ux_skills_normalized_name",
                table: "skills",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocked_blocker",
                table: "user_blocks",
                columns: new[] { "blocked_user_id", "blocker_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocker_created_at",
                table: "user_blocks",
                columns: new[] { "blocker_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_user_blocks_blocker_blocked",
                table: "user_blocks",
                columns: new[] { "blocker_id", "blocked_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_likes_sender_created_at",
                table: "user_likes",
                columns: new[] { "sender_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_user_likes_target_sender",
                table: "user_likes",
                columns: new[] { "target_id", "sender_id" });

            migrationBuilder.CreateIndex(
                name: "ux_user_likes_sender_target",
                table: "user_likes",
                columns: new[] { "sender_id", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_matches_first_user_id",
                table: "user_matches",
                column: "first_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_matches_second_user_id",
                table: "user_matches",
                column: "second_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_matches_unordered_pair",
                table: "user_matches",
                columns: new[] { "pair_min_user_id", "pair_max_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_about_me_trgm",
                table: "user_profiles",
                column: "about_me")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_faculty_id",
                table: "user_profiles",
                column: "faculty_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_first_name_trgm",
                table: "user_profiles",
                column: "first_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_last_name_trgm",
                table: "user_profiles",
                column: "last_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_search_vector",
                table: "user_profiles",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_visibility_faculty_course",
                table: "user_profiles",
                columns: new[] { "is_visible", "faculty_id", "course" });

            migrationBuilder.CreateIndex(
                name: "ux_user_profiles_user_id",
                table: "user_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_required_skills");

            migrationBuilder.DropTable(
                name: "listing_responses");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "profile_goals");

            migrationBuilder.DropTable(
                name: "profile_interests");

            migrationBuilder.DropTable(
                name: "profile_skills");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "user_likes");

            migrationBuilder.DropTable(
                name: "user_matches");

            migrationBuilder.DropTable(
                name: "listings");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "interests");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "faculties");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
