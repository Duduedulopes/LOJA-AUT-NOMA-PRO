using System.Text.RegularExpressions;

namespace AutonomousStore.WebApi.Services;

public static class PasswordPolicy
{
    public const string Description = "A senha precisa ter pelo menos 8 caracteres, incluindo letra maiúscula, minúscula, número e caractere especial.";

    private static readonly Regex Rule = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,}$",
        RegexOptions.Compiled);

    public static bool IsValid(string? password) => !string.IsNullOrEmpty(password) && Rule.IsMatch(password);
}