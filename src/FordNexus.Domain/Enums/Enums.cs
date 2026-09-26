namespace FordNexus.Domain.Enums;

public enum UserRole
{
    Admin,
    Dealer,
    Workshop,
    Partner
}

public enum CertificationStatus
{
    Pending,
    Certified,
    Suspended
}

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}

public enum ServiceType
{
    Revision,
    OilChange,
    Brakes,
    Suspension,
    Tires,
    Diagnostics,
    Recall,
    Warranty,
    Other
}
