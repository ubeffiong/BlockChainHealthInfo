using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlockChainHealthInfo.Migrations
{
    /// <inheritdoc />
    public partial class digitalSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedDate",
                table: "DbPatients",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "DbPatients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureExpiry",
                table: "DbPatients",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<byte[]>(
                name: "SignedDataBlob",
                table: "DbPatients",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotVersion",
                table: "DbPatients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedDate",
                table: "DbObservations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "DbObservations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotVersion",
                table: "DbObservations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedDate",
                table: "DbMedicalHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "DbMedicalHistories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotVersion",
                table: "DbMedicalHistories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedDate",
                table: "DbEncounters",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "DbEncounters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotVersion",
                table: "DbEncounters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordedDate",
                table: "DbPatients");

            migrationBuilder.DropColumn(
                name: "Signature",
                table: "DbPatients");

            migrationBuilder.DropColumn(
                name: "SignatureExpiry",
                table: "DbPatients");

            migrationBuilder.DropColumn(
                name: "SignedDataBlob",
                table: "DbPatients");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                table: "DbPatients");

            migrationBuilder.DropColumn(
                name: "RecordedDate",
                table: "DbObservations");

            migrationBuilder.DropColumn(
                name: "Signature",
                table: "DbObservations");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                table: "DbObservations");

            migrationBuilder.DropColumn(
                name: "RecordedDate",
                table: "DbMedicalHistories");

            migrationBuilder.DropColumn(
                name: "Signature",
                table: "DbMedicalHistories");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                table: "DbMedicalHistories");

            migrationBuilder.DropColumn(
                name: "RecordedDate",
                table: "DbEncounters");

            migrationBuilder.DropColumn(
                name: "Signature",
                table: "DbEncounters");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                table: "DbEncounters");
        }
    }
}
