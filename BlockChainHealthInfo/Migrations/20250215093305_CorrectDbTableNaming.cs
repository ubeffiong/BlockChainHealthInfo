using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlockChainHealthInfo.Migrations
{
    /// <inheritdoc />
    public partial class CorrectDbTableNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalHistories_DbEncounters_EncounterId",
                table: "MedicalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Observations_DbEncounters_EncounterId",
                table: "Observations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Observations",
                table: "Observations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MedicalHistories",
                table: "MedicalHistories");

            migrationBuilder.RenameTable(
                name: "Observations",
                newName: "DbObservations");

            migrationBuilder.RenameTable(
                name: "MedicalHistories",
                newName: "DbMedicalHistories");

            migrationBuilder.RenameIndex(
                name: "IX_Observations_EncounterId",
                table: "DbObservations",
                newName: "IX_DbObservations_EncounterId");

            migrationBuilder.RenameIndex(
                name: "IX_MedicalHistories_EncounterId",
                table: "DbMedicalHistories",
                newName: "IX_DbMedicalHistories_EncounterId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbObservations",
                table: "DbObservations",
                column: "ObservationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMedicalHistories",
                table: "DbMedicalHistories",
                column: "MedicalHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DbMedicalHistories_DbEncounters_EncounterId",
                table: "DbMedicalHistories",
                column: "EncounterId",
                principalTable: "DbEncounters",
                principalColumn: "EncounterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DbObservations_DbEncounters_EncounterId",
                table: "DbObservations",
                column: "EncounterId",
                principalTable: "DbEncounters",
                principalColumn: "EncounterId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DbMedicalHistories_DbEncounters_EncounterId",
                table: "DbMedicalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_DbObservations_DbEncounters_EncounterId",
                table: "DbObservations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DbObservations",
                table: "DbObservations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMedicalHistories",
                table: "DbMedicalHistories");

            migrationBuilder.RenameTable(
                name: "DbObservations",
                newName: "Observations");

            migrationBuilder.RenameTable(
                name: "DbMedicalHistories",
                newName: "MedicalHistories");

            migrationBuilder.RenameIndex(
                name: "IX_DbObservations_EncounterId",
                table: "Observations",
                newName: "IX_Observations_EncounterId");

            migrationBuilder.RenameIndex(
                name: "IX_DbMedicalHistories_EncounterId",
                table: "MedicalHistories",
                newName: "IX_MedicalHistories_EncounterId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Observations",
                table: "Observations",
                column: "ObservationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MedicalHistories",
                table: "MedicalHistories",
                column: "MedicalHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalHistories_DbEncounters_EncounterId",
                table: "MedicalHistories",
                column: "EncounterId",
                principalTable: "DbEncounters",
                principalColumn: "EncounterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Observations_DbEncounters_EncounterId",
                table: "Observations",
                column: "EncounterId",
                principalTable: "DbEncounters",
                principalColumn: "EncounterId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
