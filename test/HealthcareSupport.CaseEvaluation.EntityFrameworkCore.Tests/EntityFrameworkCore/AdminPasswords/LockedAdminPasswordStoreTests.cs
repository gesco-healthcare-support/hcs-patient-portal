using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 task 4 -- the decorator, proven against an in-process lock double. The REAL lock is proven
/// separately against SQL Server in <see cref="SqlAppLockTests"/>; this class is about the decorator
/// doing the right thing with whatever lock it is given.
/// </summary>
public sealed class LockedAdminPasswordStoreTests
{
    private static readonly Guid AnOffice = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    /// <summary>
    /// The decorator must not call the store before the lock is held, or after it is released.
    /// Recorded as an order rather than asserted on a flag, so "released before reading" fails too.
    /// </summary>
    [Fact]
    public async Task TheStoreIsReadOnlyWhileTheLockIsHeld()
    {
        var events = new List<string>();
        var storeLock = new RecordingLock(events);
        var inner = new RecordingStore(events, "value");

        await new LockedAdminPasswordStore(inner, storeLock).GetOrCreateAsync(null);

        events.ShouldBe(new[] { "acquire", "get-or-create", "release" });
    }

    /// <summary>
    /// THE important one. A lock that cannot be taken must stop the call dead. A decorator that
    /// swallowed the failure and read anyway would create a second password for a database that
    /// already has one -- which is the entire failure this lock exists to prevent, reintroduced at
    /// the one moment the lock was telling us it could not help.
    /// </summary>
    [Fact]
    public async Task AFailedLock_StopsTheCallAndNeverReachesTheStore()
    {
        var events = new List<string>();
        var inner = new RecordingStore(events, "value");

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await new LockedAdminPasswordStore(inner, new ThrowingLock()).GetOrCreateAsync(null));

        events.ShouldBeEmpty();
    }

    /// <summary>
    /// The lock is released even when the store throws, or one failed creation would block every
    /// later attempt for that database until the process restarted.
    /// </summary>
    [Fact]
    public async Task AThrowingStore_StillReleasesTheLock()
    {
        var events = new List<string>();
        var storeLock = new RecordingLock(events);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await new LockedAdminPasswordStore(new ThrowingStore(), storeLock).GetOrCreateAsync(null));

        events.ShouldBe(new[] { "acquire", "release" });
    }

    /// <summary>
    /// Two callers for the SAME database never overlap inside the store. Asserted by having the
    /// store record its own concurrency: the double's lock is a real per-name semaphore, so an
    /// unlocked decorator produces an observed overlap here.
    /// </summary>
    [Fact]
    public async Task ConcurrentCallersForOneDatabase_NeverOverlapInsideTheStore()
    {
        var storeLock = new RealInProcessLock();
        var inner = new OverlapDetectingStore();
        var store = new LockedAdminPasswordStore(inner, storeLock);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => store.GetOrCreateAsync(null))));

        inner.MaximumObservedConcurrency.ShouldBe(1);
    }

    /// <summary>
    /// The lock is per ENTRY, not global: creating one office's password must not wait on another's.
    /// Without this the first deployment of many offices serialises end to end for no reason.
    /// </summary>
    [Fact]
    public async Task DifferentDatabases_TakeDifferentLocks()
    {
        var storeLock = new RecordingLock(new List<string>());
        var store = new LockedAdminPasswordStore(new RecordingStore(new List<string>(), "v"), storeLock);

        await store.GetOrCreateAsync(null);
        await store.GetOrCreateAsync(AnOffice);

        storeLock.Names.ShouldBe(new[] { AdminPasswordNames.Host, AdminPasswordNames.For(AnOffice) });
    }

    // ---- doubles --------------------------------------------------------------------------

    private sealed class RecordingLock : IAdminPasswordStoreLock
    {
        private readonly List<string> _events;

        public RecordingLock(List<string> events)
        {
            _events = events;
        }

        public List<string> Names { get; } = new();

        public Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken cancellationToken = default)
        {
            Names.Add(name);
            _events.Add("acquire");
            return Task.FromResult<IAsyncDisposable>(new Release(_events));
        }

        private sealed class Release : IAsyncDisposable
        {
            private readonly List<string> _events;

            public Release(List<string> events)
            {
                _events = events;
            }

            public ValueTask DisposeAsync()
            {
                _events.Add("release");
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class ThrowingLock : IAdminPasswordStoreLock
    {
        public Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("the lock could not be taken");
        }
    }

    /// <summary>A genuine mutex per name, so the concurrency assertion measures the decorator.</summary>
    private sealed class RealInProcessLock : IAdminPasswordStoreLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

        public async Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken cancellationToken = default)
        {
            var semaphore = _semaphores.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            return new Release(semaphore);
        }

        private sealed class Release : IAsyncDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public Release(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public ValueTask DisposeAsync()
            {
                _semaphore.Release();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class RecordingStore : IAdminPasswordStore
    {
        private readonly List<string> _events;
        private readonly string _value;

        public RecordingStore(List<string> events, string value)
        {
            _events = events;
            _value = value;
        }

        public Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
        {
            _events.Add("get-or-create");
            return Task.FromResult(_value);
        }
    }

    private sealed class ThrowingStore : IAdminPasswordStore
    {
        public Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("the store failed");
        }
    }

    private sealed class OverlapDetectingStore : IAdminPasswordStore
    {
        private int _current;

        public int MaximumObservedConcurrency { get; private set; }

        public async Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _current);
            MaximumObservedConcurrency = Math.Max(MaximumObservedConcurrency, now);

            // Long enough that an unlocked decorator reliably overlaps here.
            await Task.Delay(20, cancellationToken);

            Interlocked.Decrement(ref _current);
            return "value";
        }
    }
}
