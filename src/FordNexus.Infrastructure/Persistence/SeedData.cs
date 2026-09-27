using FordNexus.Application.Abstractions;
using FordNexus.Domain.Entities;
using FordNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Infrastructure.Persistence;

public static class SeedData
{
    public static readonly Guid DealershipSorocabaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DealershipCampinasId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid WorkshopCertifiedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid WorkshopPendingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public const string KaVin = "9BFZH55L0G8123456";
    public const string EcoSportVin = "9BFZB55P7K8765432";
    public const string RangerVin = "8AFAR23L5NJ246810";
    public const string FiestaVin = "9BFZF26P3E8135790";
    public const string KaSedanVin = "9BFZH54S8J8975310";

    public static async Task SeedAsync(NexusDbContext db, IPasswordHasher hasher, TimeProvider time, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        if (await db.Users.AnyAsync(ct)) return;

        var now = time.GetUtcNow().UtcDateTime;

        db.Dealerships.AddRange(
            new Dealership { Id = DealershipSorocabaId, Name = "Ford Sorocaba Veículos", Cnpj = "11222333000181", City = "Sorocaba", State = "SP", ServiceBays = 8 },
            new Dealership { Id = DealershipCampinasId, Name = "Ford Campinas Automotiva", Cnpj = "44555666000172", City = "Campinas", State = "SP", ServiceBays = 12 });

        db.Workshops.AddRange(
            new Workshop { Id = WorkshopCertifiedId, Name = "Auto Center Vila Hortência", Cnpj = "77888999000163", City = "Sorocaba", State = "SP",
                           Status = CertificationStatus.Certified, CertifiedAt = now.AddMonths(-3), DistributorDealershipId = DealershipSorocabaId },
            new Workshop { Id = WorkshopPendingId, Name = "Oficina Mecânica Itu", Cnpj = "12312312000199", City = "Itu", State = "SP",
                           Status = CertificationStatus.Pending, DistributorDealershipId = DealershipSorocabaId });

        User NewUser(string name, string email, string password, UserRole role, Guid? dealership = null, Guid? workshop = null) =>
            new() { Name = name, Email = email, PasswordHash = hasher.Hash(password), Role = role, DealershipId = dealership, WorkshopId = workshop, CreatedAt = now };

        db.Users.AddRange(
            NewUser("Ana Admin Ford", "admin@fordnexus.com", "Admin@123", UserRole.Admin),
            NewUser("Carlos — Pós-venda Sorocaba", "concessionaria@fordnexus.com", "Dealer@123", UserRole.Dealer, dealership: DealershipSorocabaId),
            NewUser("Marina — Pós-venda Campinas", "campinas@fordnexus.com", "Dealer@123", UserRole.Dealer, dealership: DealershipCampinasId),
            NewUser("João — Vila Hortência", "oficina@fordnexus.com", "Oficina@123", UserRole.Workshop, workshop: WorkshopCertifiedId),
            NewUser("Pedro — Oficina Itu", "oficina.pendente@fordnexus.com", "Oficina@123", UserRole.Workshop, workshop: WorkshopPendingId),
            NewUser("Seguradora Parceira", "seguradora@fordnexus.com", "Parceiro@123", UserRole.Partner));

        var ka = NewVehicle(KaVin, "Ka SE 1.0", 2016, 71_000, "Rafael Souza", "+5515991110001", true, true, DealershipSorocabaId, telemetry: true);
        var eco = NewVehicle(EcoSportVin, "EcoSport FreeStyle 1.5", 2019, 49_500, "Juliana Lima", "+5515991110002", true, false, DealershipSorocabaId, telemetry: true);
        var ranger = NewVehicle(RangerVin, "Ranger XLS 2.2 Diesel", 2022, 23_000, "Marcos Pereira", "+5515991110003", true, true, DealershipSorocabaId);
        var fiesta = NewVehicle(FiestaVin, "Fiesta Hatch 1.6", 2014, 118_000, "Beatriz Alves", "+5515991110004", false, false, DealershipSorocabaId);
        var kaSedan = NewVehicle(KaSedanVin, "Ka Sedan 1.5", 2018, 64_000, "Lucas Martins", "+5519991110005", true, true, DealershipCampinasId);
        db.Vehicles.AddRange(ka, eco, ranger, fiesta, kaSedan);

        db.ServiceOrders.AddRange(
            Order(ka, ServiceType.Revision, "Revisão 50.000 km", 50_000, 780m, now.AddMonths(-26), dealership: DealershipSorocabaId),
            Order(ka, ServiceType.Brakes, "Troca de pastilhas dianteiras", 62_000, 420m, now.AddMonths(-14), workshop: WorkshopCertifiedId),
            Order(eco, ServiceType.Revision, "Revisão 40.000 km", 38_000, 950m, now.AddMonths(-5), dealership: DealershipSorocabaId),
            Order(ranger, ServiceType.Revision, "Revisão 20.000 km", 20_000, 1_350m, now.AddMonths(-2), dealership: DealershipSorocabaId),
            Order(fiesta, ServiceType.OilChange, "Troca de óleo e filtro", 101_000, 290m, now.AddMonths(-20), dealership: DealershipSorocabaId, genuine: false),
            Order(kaSedan, ServiceType.Revision, "Revisão 60.000 km", 60_000, 890m, now.AddMonths(-7), dealership: DealershipCampinasId));

        await db.SaveChangesAsync(ct);

        Vehicle NewVehicle(string vin, string model, int year, int km, string owner, string phone, bool contact, bool sharing, Guid dealership, bool telemetry = false) =>
            new() { Vin = vin, Model = model, ModelYear = year, CurrentMileage = km, OwnerName = owner, OwnerPhone = phone,
                    ContactConsent = contact, DataSharingConsent = sharing, TelemetryConsent = telemetry, HomeDealershipId = dealership, CreatedAt = now };

        ServiceOrder Order(Vehicle v, ServiceType type, string description, int km, decimal amount, DateTime at,
            Guid? dealership = null, Guid? workshop = null, bool genuine = true) =>
            new() { VehicleId = v.Id, ServiceType = type, Description = description, Mileage = km, Amount = amount,
                    PerformedAt = at, RegisteredAt = at, DealershipId = dealership, WorkshopId = workshop, GenuineParts = genuine };
    }
}
