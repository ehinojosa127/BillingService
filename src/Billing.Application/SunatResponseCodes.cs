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
    /// SUNAT ya recibió el comprobante y aún lo procesa (soap fault 0140).
    /// No reenviar el ZIP mientras aplique.
    /// </summary>
    public static bool IsInProcess(string? code, string? message)
    {
        if (NormalizeCode(code) is "0140" or "0098" or "98")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("documento igual en proceso", StringComparison.OrdinalIgnoreCase)
               || message.Contains("existe un documento igual", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// getStatusCdr sin CDR. No implica que el comprobante esté aceptado ni en cola:
    /// también ocurre si nunca llegó a SUNAT. No debe bloquear un reenvío por sí solo.
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
               || message.Contains("no se ha encontrado el cdr", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no existe el cdr", StringComparison.OrdinalIgnoreCase);
    }

    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // Prefer an explicit 4-digit SUNAT code embedded in faultcode (e.g. soap:Client.0140).
        var match = System.Text.RegularExpressions.Regex.Match(code, @"(?<!\d)(\d{4})(?!\d)");
        if (match.Success)
        {
            return match.Groups[1].Value;
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
