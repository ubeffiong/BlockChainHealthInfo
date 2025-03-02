﻿// <auto-generated />
using System;
using BlockChainHealthInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace BlockChainHealthInfo.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("BlockChainHealthInfo.Blockchain", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("CompressedData")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("EntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Hash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("ModifiedBy")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PreviousHash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("datetime2");

                    b.Property<byte[]>("Version")
                        .IsRequired()
                        .HasColumnType("varbinary(max)");

                    b.HasKey("Id");

                    b.HasIndex("Hash")
                        .IsUnique();

                    b.HasIndex("EntityType", "EntityId");

                    b.ToTable("Blockchains", (string)null);
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbEncounter", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("PatientId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("PeriodEnd")
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("Status");

                    b.HasKey("Id");

                    b.HasIndex("PatientId");

                    b.ToTable("DbEncounters", (string)null);
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbMedicalHistory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("DateRecorded")
                        .HasColumnType("datetime2")
                        .HasColumnName("DateRecorded");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(1000)")
                        .HasColumnName("Description");

                    b.Property<Guid>("EncounterId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("EncounterId");

                    b.ToTable("DbMedicalHistories", (string)null);
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbObservation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasColumnType("nvarchar(100)")
                        .HasColumnName("Code");

                    b.Property<DateTime>("EffectiveDateTime")
                        .HasColumnType("datetime2")
                        .HasColumnName("EffectiveDateTime");

                    b.Property<Guid>("EncounterId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("Status");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(255)")
                        .HasColumnName("Value");

                    b.HasKey("Id");

                    b.HasIndex("EncounterId");

                    b.ToTable("DbObservations", (string)null);
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbPatient", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("Active")
                        .HasColumnType("bit")
                        .HasColumnName("Active");

                    b.Property<DateTime>("BirthDate")
                        .HasColumnType("datetime2")
                        .HasColumnName("BirthDate");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasColumnType("nvarchar(100)")
                        .HasColumnName("FirstName");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasColumnType("nvarchar(100)")
                        .HasColumnName("LastName");

                    b.Property<string>("MasterIndex")
                        .IsRequired()
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("MasterIndex");

                    b.Property<string>("MiddleName")
                        .IsRequired()
                        .HasColumnType("nvarchar(100)")
                        .HasColumnName("MiddleName");

                    b.Property<string>("NIN")
                        .IsRequired()
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("NIN");

                    b.Property<string>("PatientNo")
                        .IsRequired()
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("PatientNo");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasColumnType("nvarchar(20)")
                        .HasColumnName("PhoneNumber");

                    b.HasKey("Id");

                    b.ToTable("DbPatients", (string)null);
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbEncounter", b =>
                {
                    b.HasOne("BlockChainHealthInfo.DbPatient", "DbPatients")
                        .WithMany("DbEncounters")
                        .HasForeignKey("PatientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DbPatients");
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbMedicalHistory", b =>
                {
                    b.HasOne("BlockChainHealthInfo.DbEncounter", "DbEncounters")
                        .WithMany("DbMedicalHistories")
                        .HasForeignKey("EncounterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DbEncounters");
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbObservation", b =>
                {
                    b.HasOne("BlockChainHealthInfo.DbEncounter", "DbEncounters")
                        .WithMany("DbObservations")
                        .HasForeignKey("EncounterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DbEncounters");
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbEncounter", b =>
                {
                    b.Navigation("DbMedicalHistories");

                    b.Navigation("DbObservations");
                });

            modelBuilder.Entity("BlockChainHealthInfo.DbPatient", b =>
                {
                    b.Navigation("DbEncounters");
                });
#pragma warning restore 612, 618
        }
    }
}
