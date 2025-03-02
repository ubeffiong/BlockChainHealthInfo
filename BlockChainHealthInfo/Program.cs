// See https://aka.ms/new-console-template for more information
using AutoMapper;
using BlockChainHealthInfo;
using BlockChainHealthInfo.DigitalSignatureManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;



public class Program
{
    private static IServiceProvider _serviceProvider;
    private static AuditTrailService _auditTrailService;

    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        _serviceProvider = host.Services;
        await InitializeDatabase(host);
        await RunApplication(host);

        Console.WriteLine("Healthcare Blockchain Patient Records System");
        Console.WriteLine("---------------------------------------------");

        await RunMainMenu();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
    {
        // Register DbContext
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                   .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll)
                   .AddInterceptors(provider.GetRequiredService<SignatureValidationInterceptor>());
        });

        services.AddScoped<SignatureValidationInterceptor>();

        // Register BlockchainService
        services.AddSingleton<BlockchainService>(provider =>
            new BlockchainService(
                provider.GetRequiredService<AppDbContext>(),
                Environment.GetEnvironmentVariable("smart-contract")));

        // Register BlockchainService
        //services.AddSingleton<KeyGenerator>(provider =>
        //    new KeyGenerator(
        //        //provider.GetRequiredService<AppDbContext>(),
        //        Environment.GetEnvironmentVariable("smart-contract")));

        // Register Key Management Services
        // Register SecureKeyStorage
        services.AddSingleton<IKeyStorage, SecureKeyStorage>(provider =>
            new SecureKeyStorage(
                Environment.GetEnvironmentVariable("smart-contract")));

        services.AddSingleton<IDigitalSignatureService, DigitalSignatureService>();
        services.AddHostedService<SignatureCleanupService>();
        services.AddHostedService<KeyRotationService>();

        // Register Audit Services
        services.AddSingleton<AuditLogger>();
        services.AddTransient<AuditTrailService>();




        // Register AutoMapper
        services.AddAutoMapper(typeof(MappingProfile));
    });

    private static async Task InitializeDatabase(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //await dbContext.Database.MigrateAsync();
    }

    private static async Task RunApplication(IHost host)
    {

        using var scope = host.Services.CreateScope();
        _auditTrailService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();

        Console.WriteLine("Healthcare Blockchain Patient Records System");
        await RunMainMenu();
    }

    private static async Task RunMainMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("Main Menu");
            Console.WriteLine("1. Manage Patients");
            Console.WriteLine("2. Manage Appointments");
            Console.WriteLine("3. System Administration");
            Console.WriteLine("4. Exit");
            Console.Write("Select option: ");

            switch (Console.ReadLine())
            {
                case "1":
                    await ManagePatients();
                    break;
                case "2":
                    await ManageAppointments();
                    break;
                case "3":
                    await SystemAdministration();
                    break;
                case "4":
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("Invalid option!");
                    break;
            }
        }
    }

    private static async Task SystemAdministration()
    {
        // Implement system administration tasks here.
        Console.WriteLine("System Administration not implemented.");
        Console.ReadKey();
    }

    private static async Task ManageAppointments()
    {
        // Implement appointment management here.
        Console.WriteLine("Manage Appointments not implemented.");
        Console.ReadKey();
    }

    private static async Task ManagePatients()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("Patient Management");
            Console.WriteLine("1. Create New Patient");
            Console.WriteLine("2. Edit Patient Record");
            Console.WriteLine("3. Manage Patient Encounters");
            Console.WriteLine("4. View Audit Trail History");
            Console.WriteLine("5. Create/Edit Patient and Encounter");
            Console.WriteLine("6. Back to Main Menu");
            Console.Write("Select option: ");

            switch (Console.ReadLine())
            {
                case "1":
                    await CreateNewPatient();
                    break;
                case "2":
                    await EditPatient();
                    break;
                case "3":
                    await ManageEncounters();
                    break;
                case "4":
                    await ViewPatientHistory();
                    break;
                case "5":
                    await CreateOrEditPatientAndEncounter();
                    break;
                case "6":
                    return;
                default:
                    Console.WriteLine("Invalid option!");
                    Console.ReadKey();
                    break;
            }
        }
    }

    private static async Task CreateNewPatient()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();

        var digitalSignatureService = scope.ServiceProvider.GetRequiredService<IDigitalSignatureService>();

        var patient = new DbPatient { Id = Guid.NewGuid() };

        Console.Write("Enter Patient No: ");
        patient.PatientNo = Console.ReadLine();

        Console.Write("Enter Patient NIN: ");
        patient.NIN = Console.ReadLine();

        Console.Write("Enter patient First name: ");
        patient.FirstName = Console.ReadLine();

        Console.Write("Enter patient Middle name: ");
        patient.MiddleName = Console.ReadLine();

        Console.Write("Enter patient Last name: ");
        patient.LastName = Console.ReadLine();



        // Sign the patient data
        var patientData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(patient));
        DateTime timestamp = DateTime.UtcNow; // Explicit timestamp
        var (signature, snapshotVersion) = digitalSignatureService.SignData(patientData, timestamp, TimeSpan.FromHours(1));

        // Store signature, snapshot version, and signed data
        patient.Signature = Convert.ToBase64String(signature);
        patient.SnapshotVersion = snapshotVersion;
        patient.SignedDataBlob = digitalSignatureService.GetFullSignedData(patientData, timestamp, TimeSpan.FromHours(1));

        await dbContext.DbPatients.AddAsync(patient);
        await dbContext.SaveChangesAsync();

        // Log initial creation in blockchain
        auditService.LogChanges(
            patient,
            new List<AuditEntry> { new AuditEntry("System", "Initial Creation", null) },
            "System"
        );

        Console.WriteLine("Patient created successfully!");
        Console.ReadKey();
    }

    private static async Task EditPatient()
    {
        Console.Write("Enter Patient ID: ");
        var patientId = Guid.Parse(Console.ReadLine());

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var digitalSignatureService = scope.ServiceProvider.GetRequiredService<IDigitalSignatureService>();

        var patient = await dbContext.DbPatients.FindAsync(patientId);
        if (patient == null)
        {
            Console.WriteLine("Patient not found!");
            Console.ReadKey();
            return;
        }

        // Create deep copy of original patient for audit comparison.
        var originalPatient = JsonConvert.DeserializeObject<DbPatient>(
            JsonConvert.SerializeObject(patient));

        // CRUD - editing
        Console.WriteLine("Leave blank to keep current value");

        Console.WriteLine($"Current Patient No: {patient.PatientNo}");
        Console.Write("New Patient No: ");
        var newPatientNo = Console.ReadLine();
        if (!string.IsNullOrEmpty(newPatientNo))
            patient.PatientNo = newPatientNo;

        Console.WriteLine($"Current First Name: {patient.FirstName}");
        Console.Write("New First Name: ");
        var newFirstName = Console.ReadLine();
        if (!string.IsNullOrEmpty(newFirstName))
            patient.FirstName = newFirstName;

        Console.WriteLine($"Current Last Name: {patient.LastName}");
        Console.Write("New Last Name: ");
        var newLastName = Console.ReadLine();
        if (!string.IsNullOrEmpty(newLastName))
            patient.LastName = newLastName;

        // Compare changes using reflection.
        var changes = new List<AuditEntry>();
        foreach (var prop in typeof(DbPatient).GetProperties().Where(p => p.Name != "Id"))
        {
            var oldVal = prop.GetValue(originalPatient);
            var newVal = prop.GetValue(patient);
            if (!object.Equals(oldVal, newVal))
            {
                changes.Add(new AuditEntry(prop.Name, oldVal, newVal));
            }
        }


        


        await dbContext.SaveChangesAsync();

        if (changes.Any())
        {
            var auditService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();
            auditService.LogChanges(patient, changes, "System");
        }

        
        Console.WriteLine("Patient record updated successfully!");
        Console.ReadKey();
    }

    private static async Task ManageEncounters()
    {
        Console.Write("Enter Patient ID: ");
        var patientId = Guid.Parse(Console.ReadLine());

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find patient
        var patient = await dbContext.DbPatients.FindAsync(patientId);
        if (patient == null)
        {
            Console.WriteLine("Patient not found!");
            Console.ReadKey();
            return;
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("Encounter Management for Patient: " + patient.PatientNo);
            Console.WriteLine("1. Add New Encounter");
            Console.WriteLine("2. Edit Encounter");
            Console.WriteLine("3. Back to Patient Management");
            Console.Write("Select option: ");
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await AddNewEncounter(patientId);
                    break;
                case "2":
                    await EditEncounter(patientId);
                    break;
                case "3":
                    return;
                default:
                    Console.WriteLine("Invalid option!");
                    break;
            }
        }
    }

    private static async Task AddNewEncounter(Guid patientId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();

        var encounter = new DbEncounter
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Status = FhirEncounterStatus.Arrived,
            PeriodStart = DateTime.UtcNow,
            PeriodEnd = DateTime.UtcNow.AddHours(1)
        };

        // Optionally add an Observation
        Console.Write("Would you like to add an Observation to this Encounter? (y/n): ");
        if (Console.ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            var observation = new DbObservation
            {
                Id = Guid.NewGuid(),
                Code = "OBS001",
                Status = FhirObservationStatus.Registered,
                Value = "Initial Observation",
                EffectiveDateTime = DateTime.UtcNow,
                EncounterId = encounter.Id
            };
            encounter.DbObservations.Add(observation);
        }

        await dbContext.DbEncounters.AddAsync(encounter);
        await dbContext.SaveChangesAsync();

        // Log the creation of the encounter
        auditService.LogChanges(
            encounter,
            new List<AuditEntry> { new AuditEntry("System", "Initial Creation", null) },
            "System"
        );

        Console.WriteLine("Encounter added successfully!");
        Console.ReadKey();
    }
    private static async Task EditEncounter(Guid patientId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();

        Console.Write("Enter Encounter ID to edit: ");
        var encounterId = Guid.Parse(Console.ReadLine());

        var encounter = await dbContext.DbEncounters
            .Include(e => e.DbObservations)
            .FirstOrDefaultAsync(e => e.Id == encounterId);

        if (encounter == null)
        {
            Console.WriteLine("Encounter not found!");
            Console.ReadKey();
            return;
        }

        // Create deep copy for audit comparison
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.None
        };

        var originalEncounter = JsonConvert.DeserializeObject<DbEncounter>(
            JsonConvert.SerializeObject(encounter, settings),
            settings);

        Console.WriteLine("Editing Encounter:");
        Console.WriteLine($"Current Status: {encounter.Status}");
        Console.Write("New Status (Arrived/Triaged/InProgress/Onleave/Finished): ");
        var newStatus = Console.ReadLine();
        if (!string.IsNullOrEmpty(newStatus))
        {
            if (Enum.TryParse<FhirEncounterStatus>(newStatus, true, out var parsedStatus))
            {
                encounter.Status = parsedStatus;
            }
            else
            {
                Console.WriteLine("Invalid status value, keeping current status.");
            }
        }

        // Edit observations if they exist
        if (encounter.DbObservations.Any())
        {
            var observation = encounter.DbObservations.First();
            Console.WriteLine($"Current Observation Value: {observation.Value}");
            Console.Write("New Observation Value (leave blank to keep current): ");
            var newObsValue = Console.ReadLine();
            if (!string.IsNullOrEmpty(newObsValue))
            {
                observation.Value = newObsValue;
            }
        }
        else
        {
            Console.Write("No Observations found. Add one? (y/n): ");
            if (Console.ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                var observation = new DbObservation
                {
                    Id = Guid.NewGuid(),
                    Code = "OBS_EDIT_Test",
                    Status = FhirObservationStatus.Final,
                    Value = "New observation added during edit",
                    EffectiveDateTime = DateTime.UtcNow,
                    EncounterId = encounter.Id
                };
                encounter.DbObservations.Add(observation);
            }
        }

        // Detect changes
        var changes = new List<AuditEntry>();
        foreach (var prop in typeof(DbEncounter).GetProperties()
            .Where(p => p.Name != "Id" && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType)))
        {
            var oldVal = prop.GetValue(originalEncounter);
            var newVal = prop.GetValue(encounter);
            if (!Equals(oldVal, newVal))
            {
                changes.Add(new AuditEntry(prop.Name, oldVal, newVal));
            }
        }

        // Handle collection changes
        var originalObservations = originalEncounter.DbObservations.ToList();
        var newObservations = encounter.DbObservations.ToList();

        // Check for added/removed observations
        if (originalObservations.Count != newObservations.Count)
        {
            changes.Add(new AuditEntry("Observations",
                $"Count: {originalObservations.Count}",
                $"Count: {newObservations.Count}"));
        }

        try
        {
            await dbContext.SaveChangesAsync();

            if (changes.Any())
            {
                auditService.LogChanges(
                    encounter,
                    changes,
                    "System"
                );
            }

            Console.WriteLine("Encounter updated successfully!");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Console.WriteLine("Conflict detected! Reloading data...");
            foreach (var entry in ex.Entries)
            {
                await entry.ReloadAsync();
            }
            await dbContext.SaveChangesAsync();
            Console.WriteLine("Changes saved after conflict resolution.");
        }

        Console.ReadKey();
    }

    private static async Task CreateOrEditPatientAndEncounter()
    {
        Console.Clear();
        Console.Write("Create new (C) or Edit existing (E) patient: ");
        var option = Console.ReadLine().Trim().ToUpperInvariant();

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();

        if (option == "C")
        {
            // Create new patient
            var patient = new DbPatient { Id = Guid.NewGuid() };

            Console.Write("Enter Patient No: ");
            patient.PatientNo = Console.ReadLine();

            Console.Write("First Name: ");
            patient.FirstName = Console.ReadLine();

            Console.Write("Middle Name: ");
            patient.MiddleName = Console.ReadLine();

            Console.Write("Last Name: ");
            patient.LastName = Console.ReadLine();

            await dbContext.DbPatients.AddAsync(patient);
            await dbContext.SaveChangesAsync();

            // Log creation
            auditService.LogChanges(
                patient,
                new List<AuditEntry> { new AuditEntry("System", null, "Patient Created") },
                "System"
            );

            Console.WriteLine("Patient created successfully!");

            // Add encounter
            Console.Write("Add initial encounter? (Y/N): ");
            if (Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                await AddNewEncounter(patient.Id);
            }
        }
        else if (option == "E")
        {
            // Edit existing patient
            Console.Write("Enter Patient ID: ");
            var patientId = Guid.Parse(Console.ReadLine());

            var patient = await dbContext.DbPatients
                .Include(p => p.DbEncounters)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null)
            {
                Console.WriteLine("Patient not found!");
                Console.ReadKey();
                return;
            }

            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            };

            // Create deep copy for audit
            var originalPatient = JsonConvert.DeserializeObject<DbPatient>(
                JsonConvert.SerializeObject(patient, settings));

            Console.WriteLine("Edit patient (leave blank to keep current)");
            Console.WriteLine($"Current Patient No: {patient.PatientNo}");
            patient.PatientNo = GetUpdatedValue("New Patient No: ", patient.PatientNo);

            patient.FirstName = GetUpdatedValue($"First Name ({patient.FirstName}): ", patient.FirstName);
            patient.MiddleName = GetUpdatedValue($"Middle Name ({patient.MiddleName}): ", patient.MiddleName);
            patient.LastName = GetUpdatedValue($"Last Name ({patient.LastName}): ", patient.LastName);

            // Detect changes
            var changes = new List<AuditEntry>();
            foreach (var prop in typeof(DbPatient).GetProperties()
                .Where(p => p.Name != "Id" && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType)))
            {
                var oldVal = prop.GetValue(originalPatient);
                var newVal = prop.GetValue(patient);
                if (!object.Equals(oldVal, newVal))
                {
                    changes.Add(new AuditEntry(prop.Name, oldVal, newVal));
                }
            }

            if (changes.Any())
            {
                await dbContext.SaveChangesAsync();
                auditService.LogChanges(patient, changes, "System");
                Console.WriteLine("Patient updated successfully!");
            }
            else
            {
                Console.WriteLine("No changes made to patient.");
            }

            // Manage encounters
            Console.Write("Manage encounters for this patient? (Y/N): ");
            if (Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine($"Encounters for {patient.PatientNo}");
                    Console.WriteLine("1. Add New Encounter");
                    Console.WriteLine("2. Edit Existing Encounter");
                    Console.WriteLine("3. View Encounter History");
                    Console.WriteLine("4. Return to Main Menu");
                    Console.Write("Choice: ");

                    switch (Console.ReadLine())
                    {
                        case "1":
                            await AddNewEncounter(patient.Id);
                            break;
                        case "2":
                            await EditEncounter(patient.Id);
                            break;
                        case "3":
                            await ViewEncounterHistory(patient.Id);
                            break;
                        case "4":
                            return;
                        default:
                            Console.WriteLine("Invalid option!");
                            break;
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("Invalid option. Please enter C or E.");
        }

        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    private static string GetUpdatedValue(string prompt, string currentValue)
    {
        Console.Write(prompt);
        var newValue = Console.ReadLine();
        return string.IsNullOrWhiteSpace(newValue) ? currentValue : newValue;
    }

    private static async Task ViewEncounterHistory(Guid patientId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var encounters = await dbContext.DbEncounters
            .Where(e => e.PatientId == patientId)
            .ToListAsync();

        Console.WriteLine("\nEncounter History:");
        foreach (var enc in encounters)
        {
            Console.WriteLine($"{enc.PeriodStart:yyyy-MM-dd} | {enc.Status} | ID: {enc.Id}");
        }

        Console.Write("\nView detailed audit for which encounter ID? ");
        if (Guid.TryParse(Console.ReadLine(), out var encounterId))
        {
            var auditService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();
            var history = auditService.GetEntityHistory(encounterId);

            Console.WriteLine($"\nAudit Trail for Encounter {encounterId}:");
            foreach (var entry in history)
            {
                Console.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm} | " +
                                $"{entry.Action} by {entry.ModifiedBy}\n" +
                                $"Changes: {entry.Changes}\n");
            }
        }

        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }


    private static async Task ViewPatientHistory()
    {
        Console.Write("Enter Patient ID: ");
        var entityId = Guid.Parse(Console.ReadLine());

        var history = _auditTrailService.GetEntityHistory(entityId);

        Console.Clear();
        Console.WriteLine($"Audit Trail for Entity ID {entityId}:\n");

        int recordCount = 0;
        foreach (var entry in history)
        {
            recordCount++;
            Console.WriteLine($"--- Record {recordCount} ---");
            Console.WriteLine($"Timestamp   : {entry.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Action      : {entry.Action}");
            Console.WriteLine($"Modified By : {entry.ModifiedBy}");

            // Display the Change Record (differences)
            Console.WriteLine("Change Record:");
            if (!string.IsNullOrWhiteSpace(entry.Changes))
            {
                var changeLines = entry.Changes.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in changeLines)
                {
                    Console.WriteLine($"  {line.Trim()}");
                }
            }
            else
            {
                Console.WriteLine("  No changes detected.");
            }

            // Display the Snapshot summary
            Console.WriteLine("Snapshot (Previous Instance):");
            try
            {
                var snapshotObj = JsonConvert.DeserializeObject(entry.SerializedSnapshot);
                var summary = GetSnapshotSummary(snapshotObj);
                Console.WriteLine(summary);
                Console.WriteLine("Full Snapshot JSON:");
                Console.WriteLine(JsonConvert.SerializeObject(snapshotObj, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [Error deserializing snapshot: {ex.Message}]");
            }
            Console.WriteLine(new string('-', 80));
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }

    /// <summary>
    /// Returns a summary of an object: its type, whether it has values, first property, last property, and count.
    /// </summary>
    private static string GetSnapshotSummary(object snapshot)
    {
        if (snapshot == null)
            return "[No Snapshot]";

        var type = snapshot.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.GetIndexParameters().Length == 0)
                        .ToList();
        int count = props.Count;
        string first = count > 0 ? $"{props.First().Name}: {props.First().GetValue(snapshot)}" : "";
        string last = count > 0 ? $"{props.Last().Name}: {props.Last().GetValue(snapshot)}" : "";
        return $"Type: {type.Name}\nHasValues: {props.Any(p => p.GetValue(snapshot) != null)}\nFirst: {first}\nLast: {last}\nCount: {count}";
    }


}