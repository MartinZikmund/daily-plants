using DailyPlants.Helpers;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// The Resources search box searches as you type. These tests pin the two things that makes hard:
/// a burst of keystrokes must cost one request, and a request the user has already typed past must
/// never be allowed to land on the screen.
/// </summary>
[TestClass]
public class ResourcesSearchDebounceTests
{
    private static FeedItem NewItem(string id) => new()
    {
        Id = id,
        Kind = FeedKind.Other,
        Title = id,
        Link = $"https://nutritionfacts.org/{id}/",
        Summary = "Summary."
    };

    private static FeedPage Page(params string[] ids)
        => new(ids.Select(NewItem).ToList(), FeedResultStatus.Fresh, false);

    /// <summary>A ViewModel with no pause at all: tests that want one install their own gate.</summary>
    private static ResourcesViewModel WithZeroDebounce(FakeFeedService feedService)
        => new(feedService) { SearchDebounce = TimeSpan.Zero };

    /// <summary>Mirrors the ViewModels' own resource lookup so the assertions hold in any locale.</summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    /// <summary>
    /// The pause the user takes between words. Standing in for it with a gate the test opens is
    /// what makes "typed quickly" mean the same thing here as it does on a keyboard - the burst is
    /// over before anything is allowed past, rather than before the thread pool happens to notice.
    /// </summary>
    private static TaskCompletionSource GateDebounce(ResourcesViewModel vm)
    {
        TaskCompletionSource gate = new();
        vm.DebounceDelay = (_, cancellationToken) => gate.Task.WaitAsync(cancellationToken);
        return gate;
    }

    /// <summary>No pause: a keystroke runs straight through to the service, so a test can race two.</summary>
    private static void RemoveDebounce(ResourcesViewModel vm) => vm.DebounceDelay = (_, _) => Task.CompletedTask;

    [TestMethod]
    public async Task Typing_ABurstOfKeystrokes_SearchesOnceForTheFinalText()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("turm", 1, Page("turmeric"));
        var vm = WithZeroDebounce(feedService);
        var typingStopped = GateDebounce(vm);

        vm.SearchQuery = "t";
        vm.SearchQuery = "tu";
        vm.SearchQuery = "tur";
        vm.SearchQuery = "turm";

        typingStopped.SetResult();
        await vm.AutoSearchTask!;

        feedService.SearchRequests.Should().Equal(("turm", 1));
        vm.IsSearchActive.Should().BeTrue();
        vm.SearchResults.Items.Should().ContainSingle().Which.Title.Should().Be("turmeric");
    }

    [TestMethod]
    public async Task Typing_ThreeCharacters_IsEnoughToSearchOnItsOwn()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("oat", 1, Page("oats"));
        var vm = WithZeroDebounce(feedService);
        var typingStopped = GateDebounce(vm);

        vm.SearchQuery = "oat";
        typingStopped.SetResult();
        await vm.AutoSearchTask!;

        feedService.SearchRequests.Should().Equal(("oat", 1));
        vm.SearchResults.Items.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task Typing_FewerThanThreeCharacters_FetchesNothingAndLeavesTheScreenAlone()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("beans", 1, Page("black beans"));
        var vm = WithZeroDebounce(feedService);
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        vm.SearchQuery = "t";
        vm.SearchQuery = "tu";

        vm.AutoSearchTask.Should().BeNull("nothing was queued");
        feedService.SearchRequests.Should().Equal(("beans", 1));
        vm.IsSearchActive.Should().BeTrue("a half-typed word is no reason to throw the results away");
        vm.SearchResults.Items.Should().ContainSingle().Which.Title.Should().Be("black beans");
    }

    [TestMethod]
    public async Task Typing_EmptyingTheBox_LeavesSearchMode()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("beans", 1, Page("black beans"));
        var vm = WithZeroDebounce(feedService);
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        vm.SearchQuery = string.Empty;

        vm.IsSearchActive.Should().BeFalse();
        vm.IsOverviewActive.Should().BeTrue();
        vm.SearchQuery.Should().BeEmpty();
        vm.SearchResults.Items.Should().BeEmpty();
        vm.SearchResults.Query.Should().BeEmpty();
        vm.SearchResults.IsLoading.Should().BeFalse();
        feedService.SearchRequests.Should().Equal(("beans", 1));
    }

    [TestMethod]
    public async Task Typing_ABoxOfNothingButSpaces_AlsoLeavesSearchMode()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("beans", 1, Page("black beans"));
        var vm = WithZeroDebounce(feedService);
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        vm.SearchQuery = "   ";

        vm.IsSearchActive.Should().BeFalse();
        feedService.SearchRequests.Should().Equal(("beans", 1));
    }

    [TestMethod]
    public async Task Enter_SearchesAShortQueryImmediately_AndDropsThePendingDebounce()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("oil", 1, Page("olive oil"));
        feedService.SetSearchPage("b6", 1, Page("vitamin b6"));
        var vm = WithZeroDebounce(feedService);

        // A pause that is never allowed to end: whatever typing queued cannot reach the service.
        GateDebounce(vm);
        vm.SearchQuery = "oil";

        await vm.SubmitSearchCommand.ExecuteAsync("b6");

        // Enter beats the debounce, however short the query.
        feedService.SearchRequests.Should().Equal(("b6", 1));
        vm.AutoSearchTask.Should().BeNull("the pending debounce was dropped, not left to fire later");
        vm.SearchQuery.Should().Be("b6");
        vm.IsSearchActive.Should().BeTrue();
        vm.SearchResults.Items.Should().ContainSingle().Which.Title.Should().Be("vitamin b6");
    }

    [TestMethod]
    public async Task Typing_AnAbandonedQuery_HasItsLateResponseThrownAway()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("kale", 1, Page("stale"));
        feedService.SetSearchPage("kefir", 1, Page("fresh"));
        var vm = WithZeroDebounce(feedService);
        RemoveDebounce(vm);

        // Both requests reach the service and are held there, so the abandoned one is guaranteed to
        // answer after the query that superseded it - the race, built on purpose.
        feedService.Pause();

        vm.SearchQuery = "kale";
        var abandoned = vm.AutoSearchTask!;

        vm.SearchQuery = "kefir";
        var winner = vm.AutoSearchTask!;

        feedService.Resume();
        await Task.WhenAll(abandoned, winner);

        feedService.SearchRequests.Should().Equal(("kale", 1), ("kefir", 1));
        vm.SearchResults.Query.Should().Be("kefir");
        vm.SearchResults.Items.Should().ContainSingle().Which.Title.Should().Be("fresh");
    }

    [TestMethod]
    public async Task Typing_ACancelledSearch_SurfacesNoNoticeAndNeverThrows()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("kale", 1, Page("stale"));
        feedService.SetSearchPage("kefir", 1, Page("fresh"));
        var vm = WithZeroDebounce(feedService);
        RemoveDebounce(vm);

        feedService.Pause();
        vm.SearchQuery = "kale";
        var abandoned = vm.AutoSearchTask!;
        vm.SearchQuery = "kefir";
        var winner = vm.AutoSearchTask!;
        feedService.Resume();

        await FluentActions.Awaiting(() => Task.WhenAll(abandoned, winner)).Should().NotThrowAsync();

        abandoned.IsCompletedSuccessfully.Should().BeTrue("cancellation is not a failure");
        vm.SearchResults.NoticeText.Should().BeEmpty();
        vm.SearchResults.EmptyStateText.Should().NotBe(
            Localized("Resources_SearchOffline", "Search needs a connection to NutritionFacts.org."));
        vm.SearchResults.IsLoading.Should().BeFalse("the search that won cleared the spinner");
    }

    [TestMethod]
    public async Task Typing_AfterASearch_ReplacesTheResultsRatherThanAppending()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("kale", 1, Page("kale one", "kale two"));
        feedService.SetSearchPage("kefir", 1, Page("kefir one"));
        var vm = WithZeroDebounce(feedService);

        var first = GateDebounce(vm);
        vm.SearchQuery = "kale";
        first.SetResult();
        await vm.AutoSearchTask!;

        var second = GateDebounce(vm);
        vm.SearchQuery = "kefir";
        second.SetResult();
        await vm.AutoSearchTask!;

        feedService.SearchRequests.Should().Equal(("kale", 1), ("kefir", 1));
        vm.SearchResults.Items.Should().ContainSingle().Which.Title.Should().Be("kefir one");
    }

    [TestMethod]
    public async Task SelectTab_WithASearchPending_DoesNotLetItArriveOverTheTab()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage("kale", 1, Page("kale one"));
        var vm = WithZeroDebounce(feedService);
        var neverOpens = GateDebounce(vm);

        vm.SearchQuery = "kale";
        var pending = vm.AutoSearchTask!;

        await vm.SelectTabCommand.ExecuteAsync("Blog");
        neverOpens.SetResult();
        await pending;

        feedService.SearchRequests.Should().BeEmpty();
        vm.IsSearchActive.Should().BeFalse();
        vm.ActiveList.Should().BeSameAs(vm.Blog);
        vm.AutoSearchTask.Should().BeNull();
    }
}
