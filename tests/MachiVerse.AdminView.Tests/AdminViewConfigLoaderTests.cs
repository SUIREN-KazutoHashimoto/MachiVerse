using MachiVerse.AdminView.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerse.AdminView.Tests;

[TestClass]
public sealed class AdminViewConfigLoaderTests
{
    private const string Header = """
        [meta]
        format = "machiverse-config"
        schema_version = "1.0"
        component = "admin-view"
        """;

    [TestMethod]
    public void Parse_FillsMissingDefaultedFields()
    {
        var result = AdminViewConfigLoader.Parse(Header);

        Assert.AreEqual((uint)1000, result.Config.DashboardRefreshMs);
        Assert.AreEqual((ushort)200, result.Config.LogDefaultPageSize);
        Assert.IsTrue(result.DefaultedKeys.Contains("dashboard.refresh-ms"));
        Assert.IsTrue(result.DefaultedKeys.Contains("confirmation.ux-timeout-seconds"));
    }

    [TestMethod]
    public void Parse_RejectsUnknownKeys()
    {
        var text = Header + "\n[dashboard]\nrefresh-ms = 1000\nunknown = 1\n";
        Assert.ThrowsException<AdminViewConfigException>(() => AdminViewConfigLoader.Parse(text));
    }

    [TestMethod]
    public void Parse_RejectsOutOfRangeValues()
    {
        var text = Header + "\n[dashboard]\nrefresh-ms = 10\n";
        Assert.ThrowsException<AdminViewConfigException>(() => AdminViewConfigLoader.Parse(text));
    }

    [TestMethod]
    public void Parse_RejectsWrongComponentHeader()
    {
        var text = Header.Replace("admin-view", "general-view", StringComparison.Ordinal);
        Assert.ThrowsException<AdminViewConfigException>(() => AdminViewConfigLoader.Parse(text));
    }
}
