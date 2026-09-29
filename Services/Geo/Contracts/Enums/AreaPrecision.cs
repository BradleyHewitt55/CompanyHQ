using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Geo.Contracts.Enums;

/// <summary>
/// How the coordinates behind an area were obtained. It travels with the answer so the UI can say
/// "near Kempton Park" instead of implying a pin the user never dropped.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<AreaPrecision>))]
public enum AreaPrecision
{
    /// <summary>A coarse, rounded position — the default for every unattended lookup.</summary>
    Area = 0,

    /// <summary>A precise position the user explicitly granted for this action.</summary>
    Pin = 1
}
