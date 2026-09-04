using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class LatestTeaserViewModelTests
{
    private static FeedItem NewItem(string id, FeedKind kind = FeedKind.Blog) => new()
    {
        Id = id,
        Kind = kind,
        Title = id,
        Link = $"https://nutritionfacts.org/{id}/",
        Summary = "Summary.",
        PublishedAt = DateTimeOffset.Now
    };

    [TestMethod]
    public async Task LoadAsync_ThreeItems_HasItemsIsTrue()
    {
        FakeFeedService feedService = new()
        {
            LatestAcrossFeeds = [NewItem("a"), NewItem("b", FeedKind.Videos), NewItem("c", FeedKind.Podcast)]
        };
        LatestTeaserViewModel vm = new(feedService, new FakeAppNavigator());

        await vm.LoadAsync();

        vm.Items.Should().HaveCount(3);
        vm.HasItems.Should().BeTrue();
        vm.IsLoading.Should().BeFalse();
        vm.ShowStrip.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadAsync_NoItems_StripStaysHidden()
    {
        FakeFeedService feedService = new();
        LatestTeaserViewModel vm = new(feedService, new FakeAppNavigator());

        await vm.LoadAsync();

        vm.Items.Should().BeEmpty();
        vm.HasItems.Should().BeFalse();
        vm.ShowStrip.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_ServiceThrows_StripStaysHiddenAndDoesNotThrow()
    {
        FakeFeedService feedService = new() { ExceptionToThrow = new HttpRequestException("offline") };
        LatestTeaserViewModel vm = new(feedService, new FakeAppNavigator());

        await vm.Invoking(v => v.LoadAsync()).Should().NotThrowAsync();

        vm.HasItems.Should().BeFalse();
        vm.ShowStrip.Should().BeFalse();
        vm.IsLoading.Should().BeFalse();
    }

    [TestMethod]
    public void OpenLatest_RaisesNavigationRequestWithKindName()
    {
        FakeAppNavigator navigator = new();
        LatestTeaserViewModel vm = new(new FakeFeedService(), navigator);
        AppNavigationRequest? raised = null;
        navigator.NavigationRequested += (_, request) => raised = request;

        vm.OpenLatestCommand.Execute("Videos");

        navigator.RequestCount.Should().Be(1);
        navigator.LastPageTag.Should().Be("Latest");
        navigator.LastParameter.Should().Be("Videos");
        raised.Should().NotBeNull();
        raised!.PageTag.Should().Be("Latest");
    }
}
