using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Import writes many rows into the live database. If it fails part way through it must
/// leave the database exactly as it found it, rather than reporting failure over a
/// half-applied write.
/// </summary>
[TestClass]
public class TransactionTests
{
    private string _dbPath = string.Empty;
    private FakeAppPreferences _prefs = null!;
    private SqliteDataService _service = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-Tx-{Guid.NewGuid():N}.db");
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        _service = new SqliteDataService(_prefs, _dbPath);
        await _service.InitializeAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // SQLite handle may still be released asynchronously; ignore.
        }
    }

    private static DateOnly Date(int day) => new(2026, 4, day);

    [TestMethod]
    public async Task RunInTransactionAsync_WhenTheOperationThrows_DiscardsEveryWrite()
    {
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await _service.RunInTransactionAsync(async () =>
            {
                await _service.SaveEntryAsync(new DailyEntry { Date = Date(1), ItemId = "beans", ServingsCompleted = 3 });
                await _service.SaveEntryAsync(new DailyEntry { Date = Date(2), ItemId = "beans", ServingsCompleted = 3 });
                throw new InvalidOperationException("import blew up half way");
            }));

        (await _service.GetEntryAsync(Date(1), "beans")).Should().BeNull();
        (await _service.GetEntryAsync(Date(2), "beans")).Should().BeNull();
    }

    [TestMethod]
    public async Task RunInTransactionAsync_WhenTheOperationThrows_LeavesEarlierDataIntact()
    {
        await _service.SaveEntryAsync(new DailyEntry { Date = Date(1), ItemId = "beans", ServingsCompleted = 1 });

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await _service.RunInTransactionAsync(async () =>
            {
                await _service.SaveEntryAsync(new DailyEntry { Date = Date(1), ItemId = "beans", ServingsCompleted = 3 });
                throw new InvalidOperationException("import blew up");
            }));

        var entry = await _service.GetEntryAsync(Date(1), "beans");
        entry!.ServingsCompleted.Should().Be(1, "a failed import must not clobber existing entries");
    }

    [TestMethod]
    public async Task RunInTransactionAsync_WhenTheOperationSucceeds_CommitsEveryWrite()
    {
        await _service.RunInTransactionAsync(async () =>
        {
            await _service.SaveEntryAsync(new DailyEntry { Date = Date(1), ItemId = "beans", ServingsCompleted = 3 });
            await _service.SaveWeightEntryAsync(new WeightEntry { Date = Date(1), Weight = 80 });
        });

        (await _service.GetEntryAsync(Date(1), "beans"))!.ServingsCompleted.Should().Be(3);
        (await _service.GetWeightEntryAsync(Date(1)))!.Weight.Should().Be(80);
    }

    [TestMethod]
    public async Task RunInTransactionAsync_DoesNotSwallowAWriteMadeElsewhere()
    {
        var insideTransaction = new TaskCompletionSource();
        var tapRecorded = new TaskCompletionSource();

        // An import that will fail, holding a transaction open while it does.
        var import = Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await _service.RunInTransactionAsync(async () =>
            {
                await _service.SaveEntryAsync(new DailyEntry { Date = Date(1), ItemId = "beans", ServingsCompleted = 3 });
                insideTransaction.SetResult();
                await tapRecorded.Task;
                throw new InvalidOperationException("import blew up half way");
            }));

        await insideTransaction.Task;

        // The user taps a serving while the import is running. Same singleton service.
        var tap = _service.SaveEntryAsync(new DailyEntry { Date = Date(2), ItemId = "beans", ServingsCompleted = 1 });
        tap.IsCompleted.Should().BeFalse("the tap has to wait for the transaction rather than join it");

        tapRecorded.SetResult();
        await import;
        await tap;

        (await _service.GetEntryAsync(Date(1), "beans")).Should().BeNull("the import rolled back");
        (await _service.GetEntryAsync(Date(2), "beans"))
            .Should().NotBeNull("the user's own tap had nothing to do with the import");
    }
}
