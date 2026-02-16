using HeadquartersService.Models;

namespace HeadquartersService.Services;

public class DispatchQueue : IDispatchQueue
{
    private readonly List<DispatchRequest> _inner = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public void Enqueue(DispatchRequest req)
    {
        // calculate priority
        req.Priority = req.FullnessPercentage / 100.0 * 10.0;
        req.EnqueuedAtUtc = DateTime.UtcNow;
        _lock.EnterWriteLock();
        try
        {
            _inner.Add(req);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryDequeue(out DispatchRequest? req)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            var ordered = _inner
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.EnqueuedAtUtc)
                .ToList();

            if (ordered.Count == 0)
            {
                req = null;
                return false;
            }

            var toReturn = ordered.First();
            _lock.EnterWriteLock();
            try
            {
                _inner.Remove(toReturn);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            req = toReturn;
            return true;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public IReadOnlyList<DispatchRequest> PeekAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _inner
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.EnqueuedAtUtc)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public DispatchRequest? Get(Guid id)
    {
        _lock.EnterReadLock();
        try
        {
            return _inner.FirstOrDefault(d => d.Id == id);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Reset()
    {
        _lock.EnterWriteLock();
        try
        {
            _inner.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}