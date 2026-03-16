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
                    services.AddSingleton<IPreferences, Preferences>();
                    services.AddSingleton<IAppPreferences, AppPreferences>();
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
        try
        {
            var dataService = Host.Services.GetRequiredService<IDataService>();
            await dataService.InitializeAsync();

            // Initialize achievement service
            var achievementService = Host.Services.GetRequiredService<IAchievementService>();
            await achievementService.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex}");
            // Continue app startup - features depending on DB will handle errors gracefully
        }

        // Initialize localization (must be done before UI is created)
        var localizationService = Host.Services.GetRequiredService<ILocalizationService>();
        await localizationService.InitializeAsync();

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
    }
}
