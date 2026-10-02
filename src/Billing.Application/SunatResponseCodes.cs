namespace Billing.Application.Commands;

public static class SunatResponseCodes
{
    public static bool IsAlreadyReported(string? code, string? message)
    {
        if (NormalizeCode(code) is "1032" or "1033")
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

    /// <summary>
    /// SUNAT ya recibió el comprobante y aún lo procesa (p. ej. soap fault 0140).
    /// No debe marcarse como error de comunicación ni reenviarse el ZIP.
    /// </summary>
    public static bool IsInProcess(string? code, string? message)
    {
        if (NormalizeCode(code) is "0140" or "98")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("en proceso", StringComparison.OrdinalIgnoreCase)
               || message.Contains("siendo procesado", StringComparison.OrdinalIgnoreCase)
               || message.Contains("documento igual en proceso", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// getStatusCdr sin CDR todavía (p. ej. 0127). El envío puede existir; no es fallo de red.
    /// </summary>
    public static bool IsCdrNotReady(string? code, string? message)
    {
        if (NormalizeCode(code) is "0127")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("no existe cdr", StringComparison.OrdinalIgnoreCase)
               || message.Contains("cdr no existe", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no se encuentra el cdr", StringComparison.OrdinalIgnoreCase)
               || message.Contains("el ticket no existe", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no se ha encontrado el cdr", StringComparison.OrdinalIgnoreCase);
    }

    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.Length > 4)
        {
            digits = digits[^4..];
        }

        return digits.PadLeft(4, '0');
    }
}
