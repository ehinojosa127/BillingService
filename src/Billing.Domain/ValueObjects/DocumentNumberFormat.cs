namespace Billing.Domain.ValueObjects;

public static class DocumentNumberFormat
{
    public const int Digits = 5;

    public static string FormatNumber(int number) =>
        number.ToString($"D{Digits}");

    public static string Combine(string series, int number) =>
        $"{series}-{FormatNumber(number)}";
}
