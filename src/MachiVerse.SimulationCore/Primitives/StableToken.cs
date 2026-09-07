namespace MachiVerse.SimulationCore.Primitives;

public readonly record struct StableToken
{
    private readonly string? _value;

    private StableToken(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public static StableToken Parse(string value)
    {
        if (!TryParse(value, out var token))
        {
            throw new FormatException("StableToken must match [a-z0-9][a-z0-9._/-]{0,63}.");
        }

        return token;
    }

    public static bool TryParse(string? value, out StableToken token)
    {
        token = default;
        if (string.IsNullOrEmpty(value) || value.Length > 64)
        {
            return false;
        }

        if (!IsLowerAsciiAlphaNumeric(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var ch = value[index];
            if (!IsLowerAsciiAlphaNumeric(ch) && ch is not '.' and not '_' and not '/' and not '-')
            {
                return false;
            }
        }

        token = new StableToken(value);
        return true;
    }

    private static bool IsLowerAsciiAlphaNumeric(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';

    public override string ToString() => Value;
}
