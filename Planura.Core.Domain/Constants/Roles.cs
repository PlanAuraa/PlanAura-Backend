namespace Planura.Core.Domain.Constants;

public static class Roles
{
    public const string Admin = "admin";
    public const string Vendor = "vendor";
    public const string Client = "client";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Vendor, Client };
}
