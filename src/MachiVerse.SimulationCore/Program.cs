using MachiVerse.SimulationCore;
using MachiVerse.SimulationCore.Primitives;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    return ContractSelfTest.Run();
}

var componentToken = StableToken.Parse("simulation-core");
Console.WriteLine($"MachiVerse {componentToken} scaffold ready.");
return 0;
