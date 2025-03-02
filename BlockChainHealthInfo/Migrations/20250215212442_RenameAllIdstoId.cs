using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlockChainHealthInfo.Migrations
{
    /// <inheritdoc />
    public partial class RenameAllIdstoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "DbPatients",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ObservationId",
                table: "DbObservations",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "MedicalHistoryId",
                table: "DbMedicalHistories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "EncounterId",
                table: "DbEncounters",
                newName: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "DbPatients",
                newName: "PatientId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "DbObservations",
                newName: "ObservationId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "DbMedicalHistories",
                newName: "MedicalHistoryId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "DbEncounters",
                newName: "EncounterId");
        }
    }
}
