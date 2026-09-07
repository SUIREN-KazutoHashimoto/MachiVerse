using Google.Protobuf;
using MachiVerse.AdminView.Protocol;
using MachiVerse.Protocol.V1;

namespace MachiVerse.AdminView.Session;

public sealed class AdminSessionState
{
    private IReadOnlySet<string> _permissions = new HashSet<string>(StringComparer.Ordinal);

    public event Action? Changed;

    public bool HasSession { get; private set; }
    public ByteString? SessionId { get; private set; }
    public ulong SessionGeneration { get; private set; }
    public SessionWireStatusV1 Status { get; private set; } = SessionWireStatusV1.Unspecified;
    public string EffectiveRoleSet { get; private set; } = string.Empty;
    public IReadOnlySet<string> EffectivePermissions => _permissions;

    public bool Apply(AuthSessionStateV1 state)
    {
        ProtocolEnvelopeValidator.ValidateId128(state.SessionId, "session_id");
        if (state.AuthDomain != AuthDomainWireV1.AdminView)
        {
            throw new ProtocolValidationException("Gateway returned a non-Admin auth domain to Administration View.");
        }

        if (HasSession && state.SessionGeneration < SessionGeneration)
        {
            return false;
        }

        SessionId = state.SessionId;
        SessionGeneration = state.SessionGeneration;
        Status = state.Status;
        EffectiveRoleSet = state.EffectiveRoleSet;
        _permissions = state.EffectivePermissions.ToHashSet(StringComparer.Ordinal);
        HasSession = true;
        Changed?.Invoke();
        return true;
    }

    public bool HasPermission(string permission) => _permissions.Contains(permission);

    public void Clear()
    {
        HasSession = false;
        SessionId = null;
        SessionGeneration = 0;
        Status = SessionWireStatusV1.Unspecified;
        EffectiveRoleSet = string.Empty;
        _permissions = new HashSet<string>(StringComparer.Ordinal);
        Changed?.Invoke();
    }
}
