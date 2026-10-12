namespace HealthcareSupport.CaseEvaluation.Logging;

/// <summary>
/// The rolling-file bounds for the host log sinks, defined ONCE (#775).
/// </summary>
/// <remarks>
/// <para>Both hosts ran their file sink on <c>Serilog.Sinks.File</c> defaults:
/// <c>rollingInterval</c> Infinite, <c>fileSizeLimitBytes</c> 1 GB,
/// <c>rollOnFileSizeLimit</c> <b>false</b>. That combination does not fill the
/// disk -- it STOPS LOGGING at the ceiling, with no error and nothing else
/// affected. Either default alone is survivable; a cap without rolling is the
/// one that ends quietly.</para>
///
/// <para>Reproduced rather than taken from documentation, using a 16 KB
/// stand-in for the 1 GB cap so the mechanism is observable in seconds. Writing
/// 4,000 events to each sink: on the defaults 16,450 bytes landed in one file
/// and the rest were discarded; with rolling and retention, 43,497 bytes landed
/// across 3 files and the count held at 3.</para>
///
/// <para>MEASURED ON THE BOX, 2026-09-11, rather than estimated. The API
/// container started 2026-08-31 18:51:28 and its log was 101,686,218 bytes
/// 11.16 days later -- about 9.1 MB/day at <c>MinimumLevel.Information</c> with
/// ASP.NET Core request logging on. That is roughly 106 days to the 1 GB
/// ceiling.</para>
///
/// <para>50 MB per file is therefore about 5.5 days of normal traffic: an
/// ordinary day never rolls on size, and a burst rolls instead of ending. 31
/// files bounds the directory at 1.55 GB worst case and about 280 MB in
/// practice, against 28 GB free on the box. Both numbers are one edit away if
/// that balance changes; the arithmetic is here so the next person does not
/// have to redo the measurement to move them.</para>
///
/// <para>WHAT THIS BOUND DOES NOT DO, and for a long time the more valuable
/// half: it does not make the logs OUTLIVE the container. Until #908 nothing
/// mounted <c>Logs/</c>, so the directory was container-local and was destroyed
/// every time a container was recreated -- which is every deploy. At 9.1 MB/day
/// no container had ever lived close to the ceiling, so in practice the logs
/// were lost to deploys long before they could be lost to this bound.</para>
///
/// <para>FIXED in #908: <c>docker-compose.prod.yml</c> mounts a NAMED volume at
/// <c>/app/Logs</c> for both <c>api</c> and <c>authserver</c>. That changes what
/// the numbers above mean -- they now describe a real steady state rather than
/// a ceiling nothing reached, because the directory stops resetting on each
/// deploy. Measured while fixing it, and worth keeping: the <c>json-file</c>
/// logging driver does not cover this either. After <c>up -d --force-recreate</c>
/// the container id changes and <c>docker logs</c> returns only the new
/// container's output, so <c>docker logs</c> and the file sink were failing the
/// same way for the same reason. <c>ProductionComposeLogPersistenceTests</c>
/// guards the mount, the Dockerfile ownership it is initialised from, and that
/// it stays a named volume rather than a bind mount.</para>
///
/// <para>Lives in Domain.Shared, alongside <c>ExternalRoleConsts</c> and for
/// the same reason: two hosts need the same numbers and neither owns them more
/// than the other. Holding them here keeps the rationale in one place rather
/// than duplicated into both <c>Program.cs</c> files, where the two copies
/// could drift with nothing to notice.</para>
/// </remarks>
public static class LogFileConsts
{
    /// <summary>
    /// Bytes per log file before it rolls. About 5.5 days of normal traffic.
    /// </summary>
    public const long SizeLimitBytes = 50L * 1024 * 1024;

    /// <summary>
    /// How many rolled files to keep. 31 bounds the directory at
    /// <see cref="SizeLimitBytes"/> x 31 worst case.
    /// </summary>
    public const int RetainedFileCount = 31;
}
