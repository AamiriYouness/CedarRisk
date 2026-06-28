using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CedarRisk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTauxCatNatRC_RemoveTauxCatNatGC_FromReferentiel_ModeTarifTauxSurCapital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "taux_catnat_gc",
                table: "referentiel_tarifaires");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "taux_catnat_gc",
                table: "referentiel_tarifaires",
                type: "numeric(6,5)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
