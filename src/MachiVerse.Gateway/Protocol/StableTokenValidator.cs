namespace MachiVerse.Gateway.Protocol;

public static class StableTokenValidator
{
    public static bool IsValid(string? value)
    {
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

        return true;
    }

    private static bool IsLowerAsciiAlphaNumeric(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';
}
