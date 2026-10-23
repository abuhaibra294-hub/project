namespace DareCare.Domain;

public static class AppRoles
{
    public const string Patient = "Patient";
    public const string Nurse = "Nurse";
    public const string Pharmacy = "Pharmacy";
    public const string Admin = "Admin";

    public static readonly string[] All = [Patient, Nurse, Pharmacy, Admin];
}
