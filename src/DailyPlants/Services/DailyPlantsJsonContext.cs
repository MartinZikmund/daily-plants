using System.Text.Json.Serialization;
using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Source-generated JSON metadata for everything the app persists. The trimmed Windows package
/// runs with reflection-based serialization switched off, so every type needs an entry here.
/// </summary>
[JsonSerializable(typeof(ExportData))]
[JsonSerializable(typeof(CachedFeed))]
internal sealed partial class DailyPlantsJsonContext : JsonSerializerContext
{
}
