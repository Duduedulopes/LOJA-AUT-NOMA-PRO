namespace AutonomousStore.Domain.Common;

public static class CpfValidation
{
    /// <summary>Valida um CPF (com ou sem pontuação) usando o algoritmo oficial de dígitos verificadores.</summary>
    public static bool IsValid(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var digits = new string(cpf.Where(char.IsDigit).ToArray());

        if (digits.Length != 11)
            return false;

        // Rejeita sequências como 00000000000, 11111111111 etc.
        if (digits.Distinct().Count() == 1)
            return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        var firstCheckDigit = CalculateCheckDigit(numbers, 9);
        if (firstCheckDigit != numbers[9])
            return false;

        var secondCheckDigit = CalculateCheckDigit(numbers, 10);
        return secondCheckDigit == numbers[10];
    }

    /// <summary>Remove pontuação, deixando só os 11 dígitos.</summary>
    public static string Normalize(string cpf) => new(cpf.Where(char.IsDigit).ToArray());

    private static int CalculateCheckDigit(int[] numbers, int length)
    {
        var sum = 0;
        var multiplier = length + 1;

        for (var i = 0; i < length; i++)
            sum += numbers[i] * (multiplier - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
