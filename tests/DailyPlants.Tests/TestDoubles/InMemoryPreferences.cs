using MZikmund.Toolkit.WinUI.Services;

namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// In-memory test double for <see cref="IPreferences"/>. Stores values in a dictionary;
/// supports the typed Get/Set/TryGet pattern used by AppPreferences.
/// </summary>
internal sealed class InMemoryPreferences : IPreferences
{
    private readonly Dictionary<string, object?> _storage = new();

    public T Get<T>(string key, T defaultValue)
    {
        if (_storage.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_storage.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public void Set<T>(string key, T? value) => _storage[key] = value;

    public T GetComplex<T>(string key, T defaultValue) => Get(key, defaultValue);

    public bool TryGetComplex<T>(string key, out T value) => TryGet(key, out value);

    public void SetComplex<T>(string key, T? value) => Set(key, value);
}
