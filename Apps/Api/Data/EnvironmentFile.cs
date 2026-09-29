namespace Api.Data;

/// <summary>
/// Loads the repository-root <c>.env</c> into the process environment.
/// </summary>
/// <remarks>
/// Docker Compose reads that file automatically; the .NET host does not. Loading it here is what
/// makes the container and the application share one set of credentials instead of two that drift.
/// Existing environment variables win, so a deployed host is never overridden by a stray file.
/// </remarks>
public static class EnvironmentFile
{
    public static void Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                DotNetEnv.Env.Load(candidate, new DotNetEnv.LoadOptions(setEnvVars: true, clobberExistingVars: false));
                return;
            }

            directory = directory.Parent;
        }
    }
}
