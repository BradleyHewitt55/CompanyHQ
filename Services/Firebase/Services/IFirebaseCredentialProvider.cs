using FirebaseAdmin;

namespace CompanyHQ.Services.Firebase.Services;

/// <summary>Holds the one optional Firebase application used by the transport.</summary>
public interface IFirebaseCredentialProvider
{
    bool IsConfigured { get; }

    FirebaseApp? App { get; }
}
