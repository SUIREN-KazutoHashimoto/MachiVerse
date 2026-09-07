using Google.Protobuf;
using MachiVerse.AdminView.Protocol;
using MachiVerse.AdminView.Session;
using MachiVerse.Protocol.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerse.AdminView.Tests;

[TestClass]
public sealed class AdminSessionStateTests
{
    [TestMethod]
    public void Apply_AcceptsAdminDomainAndPreservesExplicitPermissions()
    {
        var state = new AdminSessionState();
        var wire = new AuthSessionStateV1
        {
            SessionId = Id(1),
            AuthDomain = AuthDomainWireV1.AdminView,
            EffectiveRoleSet = "operator",
            SessionGeneration = 4,
            Status = SessionWireStatusV1.Active,
        };
        wire.EffectivePermissions.Add("admin.health.read");

        Assert.IsTrue(state.Apply(wire));
        Assert.IsTrue(state.HasPermission("admin.health.read"));
        Assert.IsFalse(state.HasPermission("general.admin"));
    }

    [TestMethod]
    public void Apply_RejectsGeneralViewDomain()
    {
        var state = new AdminSessionState();
        var wire = new AuthSessionStateV1
        {
            SessionId = Id(1),
            AuthDomain = AuthDomainWireV1.GeneralView,
            SessionGeneration = 1,
            Status = SessionWireStatusV1.Active,
        };

        Assert.ThrowsException<ProtocolValidationException>(() => state.Apply(wire));
    }

    [TestMethod]
    public void Apply_IgnoresStaleSessionGeneration()
    {
        var state = new AdminSessionState();
        state.Apply(new AuthSessionStateV1
        {
            SessionId = Id(1),
            AuthDomain = AuthDomainWireV1.AdminView,
            SessionGeneration = 5,
            Status = SessionWireStatusV1.Active,
        });

        var applied = state.Apply(new AuthSessionStateV1
        {
            SessionId = Id(1),
            AuthDomain = AuthDomainWireV1.AdminView,
            SessionGeneration = 4,
            Status = SessionWireStatusV1.Revoked,
        });

        Assert.IsFalse(applied);
        Assert.AreEqual((ulong)5, state.SessionGeneration);
        Assert.AreEqual(SessionWireStatusV1.Active, state.Status);
    }

    private static ByteString Id(byte first)
    {
        var bytes = new byte[16];
        bytes[0] = first;
        return ByteString.CopyFrom(bytes);
    }
}
