using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Appointments.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AppointmentRepeat>))]
public enum AppointmentRepeat
{
    None,
    Daily,
    Weekly,
    Monthly
}
