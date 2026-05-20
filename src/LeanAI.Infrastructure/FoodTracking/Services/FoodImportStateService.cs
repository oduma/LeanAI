using LeanAI.Application.FoodTracking.DTOs;

namespace LeanAI.Infrastructure.FoodTracking.Services;

public sealed class FoodImportStateService
{
    private static readonly object                    _lock    = new();
    private static          IReadOnlyList<FoodItemDto>? _pending;

    public bool HasPending
    {
        get { lock (_lock) return _pending is not null; }
    }

    public void Set(IReadOnlyList<FoodItemDto> items)
    {
        lock (_lock) _pending = items;
    }

    public IReadOnlyList<FoodItemDto>? Take()
    {
        lock (_lock)
        {
            var result = _pending;
            _pending   = null;
            return result;
        }
    }
}
