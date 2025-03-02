using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Blockchain> Blockchains { get; set; }

        //public DbSet<SignedEntity> SignedEntities { get; set; }


        //public DbSet<PatientBiodata> Patients { get; set; }
        //public DbSet<MedicalAppointment> Appointments { get; set; }

        //trying out FHIR
        public DbSet<DbPatient> DbPatients { get; set; }
        public DbSet<DbEncounter> DbEncounters { get; set; }
        public DbSet<DbObservation> DbObservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blockchain>(builder =>
            {
                builder.ToTable("Blockchains");
                builder.HasIndex(b => b.Hash).IsUnique();
                builder.HasIndex(b => new { b.EntityType, b.EntityId });
            });

            base.OnModelCreating(modelBuilder);

           



            // Configure FhirPatient
            modelBuilder.Entity<DbPatient>(entity =>
            {
               

                entity.ToTable("DbPatients");
            });

            // Configure Encounter table and relationship to Patient.
            modelBuilder.Entity<DbEncounter>(entity =>
            {
               
                entity.ToTable("DbEncounters");
                entity.HasOne(e => e.DbPatients)
                      .WithMany(p => p.DbEncounters)
                      .HasForeignKey(e => e.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Observation table and relationship to Encounter.
            modelBuilder.Entity<DbObservation>(entity =>
            {
                
                entity.ToTable("DbObservations");
                entity.HasOne(o => o.DbEncounters)
                      .WithMany(e => e.DbObservations)
                      .HasForeignKey(o => o.EncounterId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Encounter table and relationship to Patient.
            modelBuilder.Entity<DbMedicalHistory>(entity =>
            {
                
                entity.ToTable("DbMedicalHistories");
                entity.HasOne(e => e.DbEncounters)
                      .WithMany(p => p.DbMedicalHistories)
                      .HasForeignKey(e => e.EncounterId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    // Add the SignatureValidationInterceptor
        //    optionsBuilder.AddInterceptors(new SignatureValidationInterceptor());

        //}
    }



    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private static string _connectionString;

        public AppDbContext CreateDbContext()
        {
            return CreateDbContext(null);
        }

        public AppDbContext CreateDbContext(string[] args)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                LoadConnectionString();
            }

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseSqlServer(_connectionString);

            return new AppDbContext(builder.Options);
        }

        private static void LoadConnectionString()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json", optional: false);

            var configuration = builder.Build();

            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
    }

}
