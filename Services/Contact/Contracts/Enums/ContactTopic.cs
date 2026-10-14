using System.Text.Json.Serialization;

namespace CompanyHQ.Contact.Contracts.Enums;

/// <summary>What a visitor is writing in about, used to route and subject-line the message.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContactTopic>))]
public enum ContactTopic
{
    General,
    Support,
    Partnership,
    Press,
    Privacy
}
