using System.Security.Cryptography;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace ModernOverlay.Integration;

public sealed record NamedPipeOverlayCommandSecurity
{
    public const int MaxCommandTokenLength = 256;

    public static NamedPipeOverlayCommandSecurity None { get; } = new();

    public string? RequiredCommandToken { get; init; }

    public PipeSecurity? PipeSecurity { get; init; }

    public bool RequiresCommandToken => !string.IsNullOrEmpty(RequiredCommandToken);

    public static NamedPipeOverlayCommandSecurity RequireCommandToken(string token)
    {
        ValidateToken(token);
        return new NamedPipeOverlayCommandSecurity
        {
            RequiredCommandToken = token,
        };
    }

    public static NamedPipeOverlayCommandSecurity CurrentUserOnly(string? token = null)
    {
        if (token is not null)
        {
            ValidateToken(token);
        }

        return new NamedPipeOverlayCommandSecurity
        {
            RequiredCommandToken = token,
            PipeSecurity = CreateCurrentUserPipeSecurity(),
        };
    }

    public static NamedPipeOverlayCommandSecurity WithPipeSecurity(PipeSecurity pipeSecurity, string? token = null)
    {
        ArgumentNullException.ThrowIfNull(pipeSecurity);
        if (token is not null)
        {
            ValidateToken(token);
        }

        return new NamedPipeOverlayCommandSecurity
        {
            RequiredCommandToken = token,
            PipeSecurity = pipeSecurity,
        };
    }

    internal bool IsAuthorized(string? commandToken)
    {
        if (!RequiresCommandToken)
        {
            return true;
        }

        if (string.IsNullOrEmpty(commandToken))
        {
            return false;
        }

        byte[] expected = Encoding.UTF8.GetBytes(RequiredCommandToken!);
        byte[] actual = Encoding.UTF8.GetBytes(commandToken);
        return actual.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static void ValidateToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (token.Length > MaxCommandTokenLength)
        {
            throw new ArgumentException($"Command tokens cannot exceed {MaxCommandTokenLength} characters.", nameof(token));
        }
    }

    private static PipeSecurity CreateCurrentUserPipeSecurity()
    {
        SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows identity does not expose a user security identifier.");
        PipeSecurity pipeSecurity = new();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        return pipeSecurity;
    }
}
