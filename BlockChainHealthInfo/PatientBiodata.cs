using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.Fhir.Serialization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using static Hl7.Fhir.Model.Encounter;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography;

namespace BlockChainHealthInfo
{
    //public class PatientBiodata
    //{
    //    public Guid Id { get; set; }
    //    public string PatientNumber { get; set; }
    //    public string Name { get; set; }
    //    public int Age { get; set; }
    //    public string MedicalHistory { get; set; }
    //}

    //public class MedicalAppointment
    //{
    //    public Guid Id { get; set; }
    //    public DateTime AppointmentTime { get; set; }
    //    public string Provider { get; set; }
    //    public string Notes { get; set; }
    //}


    // Database Entity (EF Core Model)
    [Table("DbPatients")]
    public class DbPatient : IAuditableEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        // Database columns for identifiers
        [Column("PatientNo", TypeName = "nvarchar(50)")]
        public string PatientNo { get; set; } = string.Empty;

        [Column("NIN", TypeName = "nvarchar(50)")]
        public string NIN { get; set; } = string.Empty;

        [Column("MasterIndex", TypeName = "nvarchar(50)")]
        public string MasterIndex { get; set; } = string.Empty;

        // Name components
        [Column("FirstName", TypeName = "nvarchar(100)")]
        public string FirstName { get; set; } = string.Empty;

        [Column("MiddleName", TypeName = "nvarchar(100)")]
        public string MiddleName { get; set; } = string.Empty;

        [Column("LastName", TypeName = "nvarchar(100)")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Column("BirthDate")]
        public DateTime BirthDate { get; set; } = DateTime.Now;

        

        [Column("Active")]
        public bool Active { get; set; } = true;

        [Column("PhoneNumber", TypeName = "nvarchar(20)")]
        public string PhoneNumber { get; set; } = string.Empty;

        // Navigation property
        public List<DbEncounter> DbEncounters { get; set; } = new List<DbEncounter>();

        
       
    }

    // FHIR Resource Representation (Separate Class)
    [FhirType("Patient")]
    public class PatientResource : Patient
    {
        public PatientResource() { }

        public PatientResource(DbPatient entity)
        {
            Id = entity.Id.ToString();
            Meta = new Meta { LastUpdated = DateTimeOffset.UtcNow };

            // Map identifiers
            Identifier = new List<Identifier>();
            if (!string.IsNullOrEmpty(entity.PatientNo))
                Identifier.Add(CreateIdentifier("http://example.org/PatientNo", entity.PatientNo));

            if (!string.IsNullOrEmpty(entity.NIN))
                Identifier.Add(CreateIdentifier("http://example.org/NIN", entity.NIN));

            if (!string.IsNullOrEmpty(entity.MasterIndex))
                Identifier.Add(CreateIdentifier("http://example.org/MasterIndex", entity.MasterIndex));

            // Map name
            Name = new List<HumanName>
            {
                new HumanName
                {
                    Given = new[] { entity.FirstName, entity.MiddleName },
                    Family = entity.LastName
                }
            };

            BirthDate = entity.BirthDate.ToString("yyyy-MM-dd");
            Active = entity.Active;

            // Map telecom
            if (!string.IsNullOrEmpty(entity.PhoneNumber))
            {
                Telecom = new List<ContactPoint>
                {
                    new ContactPoint
                    {
                        System = ContactPoint.ContactPointSystem.Phone,
                        Value = entity.PhoneNumber,
                        Use = ContactPoint.ContactPointUse.Mobile
                    }
                };
            }
        }

        private Identifier CreateIdentifier(string system, string value) => new Identifier
        {
            System = system,
            Value = value
        };

        // Conversion method to update entity from FHIR resource
        public void UpdateEntity(DbPatient entity)
        {
            entity.PatientNo = Identifier.FirstOrDefault(i =>
                i.System == "http://example.org/PatientNo")?.Value;

            entity.NIN = Identifier.FirstOrDefault(i =>
                i.System == "http://example.org/NIN")?.Value;

            entity.MasterIndex = Identifier.FirstOrDefault(i =>
                i.System == "http://example.org/MasterIndex")?.Value;

            var primaryName = Name.FirstOrDefault();
            if (primaryName != null)
                {
                    entity.FirstName = primaryName.Given.FirstOrDefault();
                    entity.MiddleName = primaryName.Given.Count() > 1 ? primaryName.Given.ElementAt(1) : null;
                    entity.LastName = primaryName.Family;
                }

            entity.BirthDate = DateTime.Parse(BirthDate);
            entity.Active = (bool)Active;
            entity.PhoneNumber = Telecom.FirstOrDefault(t =>
                t.System == ContactPoint.ContactPointSystem.Phone)?.Value;
        }
    }

    [Table("DbEncounters")]
    public class DbEncounter : IAuditableEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Column("Status", TypeName = "nvarchar(50)")]
        public FhirEncounterStatus Status { get; set; }

        [Column("PeriodStart")]
        public DateTime PeriodStart { get; set; }

        [Column("PeriodEnd")]
        public DateTime PeriodEnd { get; set; }

        // Foreign key and navigation to Patient
        public Guid PatientId { get; set; }
        public DbPatient DbPatients { get; set; }

        // Navigation properties
        public List<DbObservation> DbObservations { get; set; } = new List<DbObservation>();
        public List<DbMedicalHistory> DbMedicalHistories { get; set; } = new List<DbMedicalHistory>();

        
    }

    [FhirType("Encounter")]
    public class EncounterResource : Encounter
    {
        private const string CustomNoteExtensionUrl = "http://example.org/fhir/StructureDefinition/medical-history-notes";
        public EncounterResource() { }

        public EncounterResource(DbEncounter entity)
        {
            Id = entity.Id.ToString();
            Meta = new Meta { LastUpdated = DateTimeOffset.UtcNow };

            Status = (EncounterStatus?)entity.Status;
            Period = new Period
            {
                Start = entity.PeriodStart.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                End = entity.PeriodEnd.ToString("yyyy-MM-ddTHH:mm:sszzz")
            };

            Subject = new ResourceReference($"Patient/{entity.PatientId}");

            // Map observations
            if (entity.DbObservations.Any())
            {
                ObservationReferences = entity.DbObservations
                    .Select(o => new ResourceReference($"Observation/{o.Id}"))
                    .ToList();
            }

            if (entity.DbMedicalHistories.Any())
            {
                Extension.AddRange(entity.DbMedicalHistories.Select(mh =>
                    new Extension(
                        CustomNoteExtensionUrl,
                        new Hl7.Fhir.Model.Annotation
                        {
                            Text = mh.Description,
                            Time = mh.DateRecorded.ToString("yyyy-MM-ddTHH:mm:sszzz")
                        }
                    )
                ));
            }
        }

        // Add a new property to avoid conflict
        public List<ResourceReference> ObservationReferences { get; set; } = new List<ResourceReference>();


        public void UpdateEntity(DbEncounter entity)
        {
            entity.Status = (FhirEncounterStatus)Status;
            entity.PeriodStart = DateTime.Parse(Period.Start);
            entity.PeriodEnd = DateTime.Parse(Period.End);

            // Update custom medical history notes from extensions
            var medicalHistories = this.Extension
                .Where(e => e.Url == CustomNoteExtensionUrl)
                .Select(e => (Hl7.Fhir.Model.Annotation)e.Value)
                .ToList();

            entity.DbMedicalHistories = medicalHistories.Select(mh => new DbMedicalHistory
            {
                Description = mh.Text,
                DateRecorded = DateTime.Parse(mh.Time)
            }).ToList();
        }
    }

    public enum FhirEncounterStatus
    {
        Planned,    // The encounter has been planned but has not yet started.
        Arrived,    // The patient has arrived for the encounter.
        InProgress, // The encounter is currently ongoing.
        OnLeave,    // The encounter is on hold (e.g., the patient is temporarily on leave).
        Finished,   // The encounter has ended.
        Cancelled   // The encounter was cancelled.
    }
    public enum FhirObservationStatus
    {
        Registered, // The existence of the observation is registered, but there is no result yet.
        Preliminary, // This is an initial or interim observation.
        Final,      // The observation is complete and verified.
        Amended,    // The observation has been modified after it was finalized.
        Cancelled   // The observation is no longer valid.
    }


    [Table("DbObservations")]
    public class DbObservation : IAuditableEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Column("Status", TypeName = "nvarchar(50)")]
        public FhirObservationStatus Status { get; set; }

        [Column("Code", TypeName = "nvarchar(100)")]
        public string Code { get; set; }

        [Column("Value", TypeName = "nvarchar(255)")]
        public string Value { get; set; }

        [Column("EffectiveDateTime")]
        public DateTime EffectiveDateTime { get; set; }

        // Foreign key and navigation to Encounter
        public Guid EncounterId { get; set; }
        public DbEncounter DbEncounters { get; set; }

        
    }

    [FhirType("Observation")]
    public class ObservationResource : Observation
    {
        public ObservationResource() { }

        public ObservationResource(DbObservation entity)
        {
            Id = entity.Id.ToString();
            Meta = new Meta { LastUpdated = DateTimeOffset.UtcNow };

            Status = (ObservationStatus?)entity.Status;
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding { Code = entity.Code }
                }
            };

            Value = new FhirString(entity.Value);
            Effective = new FhirDateTime(entity.EffectiveDateTime.ToString("yyyy-MM-ddTHH:mm:sszzz"));

            Subject = new ResourceReference($"Encounter/{entity.EncounterId}");
        }

        public void UpdateEntity(DbObservation entity)
        {
            entity.Status = (FhirObservationStatus)Status;
            entity.Code = Code.Coding.FirstOrDefault()?.Code;
            entity.Value = Value?.ToString();
            entity.EffectiveDateTime = DateTime.Parse(Effective?.ToString());
        }
    }

    [Table("DbMedicalHistories")]
    public class DbMedicalHistory : IAuditableEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Column("Description", TypeName = "nvarchar(1000)")]
        public string Description { get; set; }

        [Column("DateRecorded")]
        public DateTime DateRecorded { get; set; }

        // Foreign key and navigation to Encounter
        public Guid EncounterId { get; set; }
        public DbEncounter DbEncounters { get; set; }

        
    }

    [FhirType("Annotation")]
    public class MedicalHistoryResource : Hl7.Fhir.Model.Annotation
    {
        public MedicalHistoryResource() { }

        public MedicalHistoryResource(DbMedicalHistory entity)
        {
            Text = entity.Description;
            Time = entity.DateRecorded.ToString("yyyy-MM-ddTHH:mm:sszzz");
        }

        public void UpdateEntity(DbMedicalHistory entity)
        {
            entity.Description = Text;
            entity.DateRecorded = DateTime.Parse(Time);
        }
    }

}
