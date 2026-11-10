namespace Nerves.Cli.CommandLine;

/// <summary>Process exit codes shared by every command.</summary>
public static class ExitCodes
{
    public const int Success = 0;

    /// <summary>A check or a migration failed; the command did what it could.</summary>
    public const int Failure = 1;

    /// <summary>The invocation itself was wrong (unknown command, bad option).</summary>
    public const int Usage = 2;
}
