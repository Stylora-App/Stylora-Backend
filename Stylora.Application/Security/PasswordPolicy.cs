namespace Stylora.Application.Security;

public static class PasswordPolicy
{
    public const string ValidationMessage =
        "Password must contain at least 8 characters, one uppercase letter, and one special character.";

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUppercase = password.Any(char.IsUpper);
        var hasSpecialCharacter = password.Any(ch => !char.IsLetterOrDigit(ch));

        return hasUppercase && hasSpecialCharacter;
    }
}
