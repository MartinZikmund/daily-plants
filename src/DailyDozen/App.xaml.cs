using DailyDozen.Services;
using DailyDozen.Views;
using Uno.Resizetizer;

namespace DailyDozen;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    public Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    /// <summary>
    /// Gets the current App instance.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider? Services => Host?.Services;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.AddSingleton<IDataService, SqliteDataService>();
                    services.AddSingleton<ILocalizationService, LocalizationService>();
                    services.AddSingleton<IAchievementService, AchievementService>();
                    services.AddTransient<IExportService, ExportService>();
                })
            );
        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();

        // Initialize the database
        var dataService = Host.Services.GetRequiredService<IDataService>();
        await dataService.InitializeAsync();

        // Initialize localization (must be done before UI is created)
        var localizationService = Host.Services.GetRequiredService<ILocalizationService>();
        await localizationService.InitializeAsync();

        // Initialize achievement service
        var achievementService = Host.Services.GetRequiredService<IAchievementService>();
        await achievementService.InitializeAsync();

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
        }

        if (rootFrame.Content == null)
        {
            // Navigate to the shell page
            rootFrame.Navigate(typeof(ShellPage), args.Arguments);
        }
        // Ensure the current window is active
        MainWindow.Activate();

        // Apply saved theme preference
        await ApplyThemeAsync(dataService);
    }

    private async Task ApplyThemeAsync(IDataService dataService)
    {
        var settings = await dataService.GetSettingsAsync();
        if (MainWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = settings.ThemePreference switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }
}
