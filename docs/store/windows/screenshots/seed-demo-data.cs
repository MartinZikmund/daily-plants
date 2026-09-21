#:package Microsoft.Data.Sqlite.Core@10.0.3
#:package SQLitePCLRaw.bundle_winsqlite3@2.1.11

using Microsoft.Data.Sqlite;

// Replaces every entry in the app's database with a believable history for store screenshots:
// a 23-day streak, today about two thirds done, a gentle weight trend and the achievements that go with it.
string dbPath = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyPlants", "dailyplants.db");

if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"No database at {dbPath}. Run the app once so it creates one.");
    return 1;
}

Dictionary<string, int> required = new()
{
    ["beans"] = 3, ["berries"] = 1, ["other_fruits"] = 3, ["greens"] = 2, ["cruciferous"] = 1, ["other_vegetables"] = 2,
    ["flaxseed"] = 1, ["nuts"] = 1, ["herbs_spices"] = 1, ["whole_grains"] = 3, ["beverages"] = 5, ["exercise"] = 1,
    ["vitamin_b12"] = 1, ["preload_water"] = 3, ["negative_calorie_preload"] = 3, ["vinegar"] = 3, ["undistracted_meals"] = 3,
    ["twenty_minute_rule"] = 3, ["front_load_calories"] = 1, ["time_restricted_eating"] = 1, ["deflour_diet"] = 1,
    ["black_cumin"] = 1, ["garlic_powder"] = 1, ["ground_ginger"] = 1, ["nutritional_yeast"] = 1, ["cumin"] = 2,
    ["green_tea"] = 3, ["stay_hydrated"] = 1, ["exercise_timing"] = 1, ["enough_sleep"] = 1, ["weigh_twice"] = 2,
    ["complete_intentions"] = 3, ["nightly_fast"] = 1, ["nightly_trendelenburg"] = 1,
};

// Mostly done, with a few rows still to go in each group.
Dictionary<string, int> today = new(required)
{
    ["beans"] = 2, ["other_fruits"] = 2, ["other_vegetables"] = 1, ["nuts"] = 0, ["whole_grains"] = 2, ["beverages"] = 3,
    ["exercise"] = 0, ["preload_water"] = 2, ["negative_calorie_preload"] = 2, ["vinegar"] = 1, ["undistracted_meals"] = 2,
    ["twenty_minute_rule"] = 2, ["time_restricted_eating"] = 0, ["ground_ginger"] = 0, ["cumin"] = 1, ["green_tea"] = 2,
    ["exercise_timing"] = 0, ["weigh_twice"] = 1, ["complete_intentions"] = 1, ["nightly_fast"] = 0, ["nightly_trendelenburg"] = 0,
};

// Days ago each achievement was earned. They are marked seen so no toast or badge shows up in the captures.
Dictionary<string, int> earned = new()
{
    ["milestone_first_day"] = 75, ["milestone_first_perfect"] = 71, ["milestone_first_week"] = 69, ["milestone_first_month"] = 46,
    ["streak_7"] = 17, ["streak_14"] = 10, ["completion_10"] = 52, ["completion_25"] = 24, ["completion_50"] = 3,
    ["item_beans_50"] = 58, ["item_berries_50"] = 51, ["item_greens_50"] = 40, ["item_whole_grains_50"] = 44,
    ["item_flaxseed_50"] = 30, ["item_cruciferous_50"] = 28, ["item_nuts_50"] = 20, ["item_exercise_50"] = 15,
};

const int Streak = 23;
const int History = 75;
Random random = new(42);
DateOnly now = DateOnly.FromDateTime(DateTime.Today);

using SqliteConnection connection = new($"Data Source={dbPath}");
connection.Open();
using SqliteTransaction transaction = connection.BeginTransaction();

void Execute(string sql, params (string Name, object? Value)[] parameters)
{
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = sql;
    foreach ((string name, object? value) in parameters)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
    command.ExecuteNonQuery();
}

foreach (string table in new[] { "DailyEntries", "WeightEntries", "EarnedAchievements", "CustomItems", "CustomItemEntries" })
{
    Execute($"DELETE FROM {table}");
}

int entries = 0;
for (int back = History; back >= 0; back--)
{
    DateOnly day = now.AddDays(-back);
    Dictionary<string, int> servings;
    if (back == 0)
    {
        servings = today;
    }
    else if (back <= Streak)
    {
        servings = required;
    }
    else if (back == Streak + 1)
    {
        // The miss that the current streak started after.
        servings = new(required) { ["nuts"] = 0, ["exercise"] = 0 };
    }
    else
    {
        double quality = new[] { 0.6, 0.75, 0.85, 0.9, 1.0 }[random.Next(5)];
        servings = required.ToDictionary(p => p.Key, p => random.NextDouble() < quality ? p.Value : random.Next(p.Value));
    }

    foreach ((string item, int count) in servings.Where(p => p.Value > 0))
    {
        Execute("INSERT INTO DailyEntries (Date, ItemId, ServingsCompleted) VALUES ($date, $item, $count)",
            ("$date", day.ToString("yyyy-MM-dd")), ("$item", item), ("$count", count));
        entries++;
    }
}

double weight = 78.6;
for (int back = History; back >= 0; back -= 3)
{
    weight += -0.16 + (random.NextDouble() * 0.45 - 0.25);
    Execute("INSERT INTO WeightEntries (Date, Weight, Notes) VALUES ($date, $weight, NULL)",
        ("$date", now.AddDays(-back).ToString("yyyy-MM-dd")), ("$weight", Math.Round(weight, 1)));
}

foreach ((string achievement, int back) in earned)
{
    DateTime at = now.AddDays(-back).ToDateTime(new TimeOnly(19, 30), DateTimeKind.Utc);
    Execute("INSERT INTO EarnedAchievements (AchievementId, EarnedAt, HasBeenSeen) VALUES ($id, $at, 1)",
        ("$id", achievement), ("$at", at.ToString("yyyy-MM-ddTHH:mm:ss.0000000Z")));
}

transaction.Commit();
Console.WriteLine($"Seeded {entries} entries into {dbPath}");
return 0;
