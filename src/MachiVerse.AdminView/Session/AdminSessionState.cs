using Google.Protobuf;
using MachiVerse.AdminView.Protocol;
using MachiVerse.Protocol.V1;

namespace MachiVerse.AdminView.Session;

public sealed class AdminSessionState
{
    private const int MaxPermissionCount = 1024;
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

        if (state.Status == SessionWireStatusV1.Unspecified)
        {
            throw new ProtocolValidationException("Gateway returned an unspecified Admin session status.");
        }

        if (state.EffectivePermissions.Count > MaxPermissionCount)
        {
            throw new ProtocolValidationException($"Admin session permissions exceed the {MaxPermissionCount} entry limit.");
        }

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (var permission in state.EffectivePermissions)
        {
            ProtocolEnvelopeValidator.ValidateStableToken(permission, "effective_permissions");
            if (previous is not null && string.CompareOrdinal(previous, permission) >= 0)
            {
                throw new ProtocolValidationException("Admin session permissions must be strictly ASCII/ordinal ascending with no duplicates.");
            }

            permissions.Add(permission);
            previous = permission;
        }

        if (HasSession && state.SessionGeneration < SessionGeneration)
        {
            return false;
        }

        SessionId = state.SessionId;
        SessionGeneration = state.SessionGeneration;
        Status = state.Status;
        EffectiveRoleSet = state.EffectiveRoleSet;
        _permissions = permissions;
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
