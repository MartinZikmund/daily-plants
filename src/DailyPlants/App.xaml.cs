using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;
using DailyPlants.Views;
using MZikmund.Toolkit.WinUI.Services;
using Uno.Resizetizer;

namespace DailyPlants;

public partial class App : Application
{
    /// <summary>
    /// Covers the window before the host exists - the handlers registered in the constructor can
    /// fire that early. Once the host is up, <see cref="Log"/> switches to its pipeline.
    /// </summary>
    private readonly ILoggerFactory _bootstrapLoggerFactory;

    private readonly ILogger<App> _bootstrapLogger;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        // Built before the host: the handlers registered below can fire before it exists.
        _bootstrapLoggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _bootstrapLogger = _bootstrapLoggerFactory.CreateLogger<App>();

        RegisterGlobalExceptionHandlers();
    }

    /// <summary>Serilog's pipeline once the host is up, and the bootstrap logger until then.</summary>
    private ILogger Log => Host?.Services.GetService<ILogger<App>>() ?? _bootstrapLogger;

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

    /// <summary>
    /// Records crashes to the local log so a user-reported failure has a trail to follow.
    /// Nothing here marks an exception handled: the goal is diagnosis, not hiding faults.
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        UnhandledException += (_, e) =>
            Log.LogError(e.Exception, "Unhandled UI exception");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.LogError(e.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // OnLaunched must be async void, so nothing may escape it.
        try
        {
            await LaunchAsync(args);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Application startup failed");
            throw;
        }
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseSerilog(consoleLoggingEnabled: true, fileLoggingEnabled: true)
                .UseLogging(configure: (context, logging) => logging
                    .SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information))
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.AddSingleton<IPreferences, Preferences>();
                    services.AddSingleton<IAppPreferences, AppPreferences>();
                    services.AddSingleton<IDataService, SqliteDataService>();
                    services.AddSingleton<ILocalizationService, LocalizationService>();
                    services.AddSingleton<IAchievementService, AchievementService>();
                    services.AddTransient<IExportService, ExportService>();

                    // Resources feed
                    services.AddSingleton(TimeProvider.System);
                    services.AddSingleton(_ =>
                    {
                        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("DailyPlants");
                        return client;
                    });
                    services.AddSingleton<IFeedCache, JsonFeedCache>();
                    services.AddSingleton<IFeedService, FeedService>();
                    services.AddSingleton<IAppNavigator, AppNavigator>();
                })
            );
        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();

        var databaseFailure = await InitializeDataAsync();

        // Initialize localization (must be done before UI is created)
        try
        {
            var localizationService = Host.Services.GetRequiredService<ILocalizationService>();
            await localizationService.InitializeAsync();
        }
        catch (Exception ex)
        {
            // The app is usable in the default language; a failure here must not block launch.
            Log.LogError(ex, "Localization initialization failed");
        }

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not ShellView windowShell)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            windowShell = new ShellView(MainWindow);

            // Place the frame in the current Window
            MainWindow.Content = windowShell;
        }

        // Apply saved theme preference
        var appPreferences = Host.Services.GetRequiredService<IAppPreferences>();
        SettingsViewModel.ApplyTheme(appPreferences.ThemePreference);

        // Ensure the current window is active
        MainWindow.Activate();

        if (databaseFailure is not null)
        {
            await ShowDatabaseFailureAsync(databaseFailure);
        }
    }

    /// <summary>
    /// Brings up the database and achievement state, returning the failure if either could
    /// not start. A dead data layer used to be swallowed into a Debug.WriteLine that is
    /// stripped from Release builds, leaving the app silently broken.
    /// </summary>
    private async Task<Exception?> InitializeDataAsync()
    {
        try
        {
            var dataService = Host!.Services.GetRequiredService<IDataService>();
            await dataService.InitializeAsync();

            var achievementService = Host.Services.GetRequiredService<IAchievementService>();
            await achievementService.InitializeAsync();

            // Achievements were previously only ever evaluated two seconds after a diary
            // tap, so anything that became true after the last tap of the day went
            // unawarded until the next one.
            await achievementService.CheckAndAwardAchievementsAsync();

            return null;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Database initialization failed");
            return ex;
        }
    }

    /// <summary>
    /// The log path is appended rather than dropped into the middle of a sentence, so that
    /// translations do not have to bend a grammatical case around a file path.
    /// </summary>
    private string DescribeDatabaseFailure(Exception failure)
    {
        // Serilog owns the file now, so the app no longer knows the path to quote.
        var explanation = Localizer.GetString(
            "Database_FailureMessageNoLog",
            "Your entries could not be loaded and changes may not be saved. Restart the app, "
                + "and if this keeps happening the details are in the log in the app data folder.");

        return explanation + Environment.NewLine + Environment.NewLine + failure.Message;
    }

    private async Task ShowDatabaseFailureAsync(Exception failure)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = Localizer.GetString(
                    "Database_FailureTitle",
                    "Daily Plants could not open your data"),
                Content = DescribeDatabaseFailure(failure),
                CloseButtonText = Localizer.GetString("Database_FailureContinue", "Continue anyway"),
                XamlRoot = MainWindow?.Content?.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Could not show the database failure dialog");
        }
    }
}
