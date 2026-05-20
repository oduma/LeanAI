using LeanAI.Application.ActivityTracking.DTOs;

namespace LeanAI.Infrastructure.ActivityTracking.Services;

public sealed class RunImportStateService
{
    private static readonly object                           _lock    = new();
    private static          IReadOnlyList<RunActivityRowDto>? _pending;

    public bool HasPending
    {
        get { lock (_lock) return _pending is not null; }
    }

    public void Set(IReadOnlyList<RunActivityRowDto> rows)
    {
        lock (_lock) _pending = rows;
    }

    public IReadOnlyList<RunActivityRowDto>? Take()
    {
        lock (_lock)
        {
            var result = _pending;
            _pending   = null;
            return result;
        }
    }
}
