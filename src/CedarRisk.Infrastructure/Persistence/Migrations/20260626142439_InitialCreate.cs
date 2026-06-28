using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CedarRisk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "garantie_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantie_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    age_limite_vehicule = table.Column<int>(type: "integer", nullable: true),
                    exigence_conjonctive = table.Column<string>(type: "jsonb", nullable: false),
                    exigence_disjonctive = table.Column<string>(type: "jsonb", nullable: false),
                    incompatibilites = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usages_exclus = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantie_conditions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "garantie_definitions",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantie_definitions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "garantie_tarifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    garantie_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    mode_tarifaire = table.Column<string>(type: "jsonb", nullable: false),
                    est_soumis_a_catnat = table.Column<bool>(type: "boolean", nullable: false),
                    taux_catnat_gc = table.Column<decimal>(type: "numeric(8,6)", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    superseded_by_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantie_tarifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referentiel_tarifaires",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    taux_catnat_rc = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                    taux_taxe_rc = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                    taux_parafiscale_rc = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                    taux_catnat_gc = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                    taux_taxe_gc = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                    taux_parafiscale_gc = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                    tarif_remorque = table.Column<string>(type: "jsonb", nullable: false),
                    timbre_cnpac = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referentiel_tarifaires", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bareme_rc",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ReferentielTarifaireId = table.Column<int>(type: "integer", nullable: false),
                    usage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    puissance_min = table.Column<int>(type: "integer", nullable: false),
                    puissance_max = table.Column<int>(type: "integer", nullable: true),
                    prime_ht = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    referentiel_tarifaire_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bareme_rc", x => x.id);
                    table.ForeignKey(
                        name: "FK_bareme_rc_referentiel_tarifaires_referentiel_tarifaire_id",
                        column: x => x.referentiel_tarifaire_id,
                        principalTable: "referentiel_tarifaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bareme_rc_referentiel_usage_puissance",
                table: "bareme_rc",
                columns: new[] { "referentiel_tarifaire_id", "usage", "puissance_min" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_garantie_conditions_code_active",
                table: "garantie_conditions",
                columns: new[] { "garantie_code", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uix_garantie_conditions_code_active",
                table: "garantie_conditions",
                column: "garantie_code",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_garantie_definitions_is_active",
                table: "garantie_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_garantie_tarifications_code_validfrom",
                table: "garantie_tarifications",
                columns: new[] { "garantie_code", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "uix_garantie_tarifications_code_open",
                table: "garantie_tarifications",
                columns: new[] { "garantie_code", "valid_to" },
                unique: true,
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_referentiel_tarifaires_valid_from",
                table: "referentiel_tarifaires",
                column: "valid_from");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bareme_rc");

            migrationBuilder.DropTable(
                name: "garantie_conditions");

            migrationBuilder.DropTable(
                name: "garantie_definitions");

            migrationBuilder.DropTable(
                name: "garantie_tarifications");

            migrationBuilder.DropTable(
                name: "referentiel_tarifaires");
        }
    }
}
