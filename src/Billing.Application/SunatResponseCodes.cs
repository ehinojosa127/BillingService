namespace Billing.Application.Commands;

internal static class SunatResponseCodes
{
    public static bool IsAlreadyReported(string? code, string? message)
    {
        if (code is "1032" or "1033")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("informado previamente", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ya fue informado", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ya se encuentra informado", StringComparison.OrdinalIgnoreCase);
    }
}
