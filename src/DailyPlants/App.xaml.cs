using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;
using DailyPlants.Views;
using MZikmund.Toolkit.WinUI.Services;
using Serilog;
using Uno.Resizetizer;

namespace DailyPlants;

public partial class App : Application
{
    /// <summary>The one Serilog sink; the host's pipeline shares it so both write the same file.</summary>
    private readonly Serilog.Core.Logger _fileLogger;

    /// <summary>Owns the provider behind <see cref="_logger"/>; the host builds its own later.</summary>
    private readonly ILoggerFactory _loggerFactory;

    private readonly ILogger<App> _logger;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        // Built before the host: the handlers registered below can fire before it exists.
        LogFilePath = LogFile.Resolve();
        _fileLogger = BuildFileLogger(LogFilePath);
        _loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(_fileLogger, dispose: false));
        _logger = _loggerFactory.CreateLogger<App>();

        RegisterGlobalExceptionHandlers();
    }

    /// <summary>The log file the sink writes to, or null on a head with no writable location.</summary>
    public string? LogFilePath { get; }

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
    /// Rolls on size rather than by date, so <see cref="LogFilePath"/> keeps naming the live file.
    /// A null path yields a sink that discards - file logging is optional, never fatal.
    /// </summary>
    private static Serilog.Core.Logger BuildFileLogger(string? path)
    {
        LoggerConfiguration configuration = new();

        if (path is not null)
        {
            configuration = configuration.WriteTo.File(
                path,
                fileSizeLimitBytes: LogFile.MaxBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: LogFile.RetainedFiles);
        }

        return configuration.CreateLogger();
    }

    /// <summary>
    /// Records crashes to the local log so a user-reported failure has a trail to follow.
    /// Nothing here marks an exception handled: the goal is diagnosis, not hiding faults.
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        UnhandledException += (_, e) =>
            _logger.LogError(e.Exception, "Unhandled UI exception");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            _logger.LogError(e.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger.LogError(e.Exception, "Unobserved task exception");
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
            _logger.LogError(ex, "Application startup failed");
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
                .UseLogging(configure: (context, logging) => logging
                    .SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information)
                    .AddSerilog(_fileLogger, dispose: false))
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
            _logger.LogError(ex, "Localization initialization failed");
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
            _logger.LogError(ex, "Database initialization failed");
            return ex;
        }
    }

    private async Task ShowDatabaseFailureAsync(Exception failure)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Daily Plants could not open your data",
                Content = "Your entries could not be loaded and changes may not be saved. "
                    + "Restart the app, and if this keeps happening the log file at "
                    + (LogFilePath ?? "the app data folder")
                    + " has the details."
                    + Environment.NewLine + Environment.NewLine
                    + failure.Message,
                CloseButtonText = "Continue anyway",
                XamlRoot = MainWindow?.Content?.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not show the database failure dialog");
        }
    }
}
