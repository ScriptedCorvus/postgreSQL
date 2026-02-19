using System.IO;
using System.Windows;
using DatabaseClient.App.Services;
using DatabaseClient.App.ViewModels;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Services;
using DatabaseClient.Data.Providers;
using DatabaseClient.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace DatabaseClient.App;

/// <summary>
/// Interaction logic for App.xaml — configures DI and launches the main window.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private readonly GlobalExceptionHandler _exceptionHandler = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog first — before anything else
        ConfigureSerilog();
        Log.Information("Application starting");

        // Register global exception handlers
        _exceptionHandler.Register(this);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Load settings before showing UI
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Apply theme from settings
        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        themeService.ApplyTheme();

        // Initialize localization from saved language preference
        LocalizationManager.Instance.SetLanguage(settingsService.Settings.Language);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Start connection health monitor
        var healthMonitor = _serviceProvider.GetRequiredService<ConnectionHealthMonitor>();
        var notificationService = _serviceProvider.GetRequiredService<INotificationService>();
        healthMonitor.HealthStatusChanged += (_, args) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                switch (args.Status)
                {
                    case ConnectionHealthStatus.Healthy:
                        notificationService.ShowSuccess(args.Message);
                        break;
                    case ConnectionHealthStatus.Reconnecting:
                        notificationService.ShowWarning(args.Message);
                        break;
                    case ConnectionHealthStatus.Failed:
                        notificationService.ShowError(args.Message);
                        break;
                }
            });
        };
        healthMonitor.Start();

        Log.Information("Application started successfully");
    }

    private static void ConfigureSerilog()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "DatabaseClient")
            .WriteTo.File(
                path: Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: 10 * 1024 * 1024) // 10 MB per file
            .WriteTo.Sink(Services.LogViewerSink.Instance)
#if DEBUG
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
#endif
            .CreateLogger();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Serilog → Microsoft.Extensions.Logging integration
        services.AddLogging(builder => builder.AddSerilog(dispose: false));

        // Core services
        services.AddSingleton<IConnectionRepository, JsonConnectionRepository>();
        services.AddSingleton<SshTunnelManager>();
        services.AddSingleton<IDatabaseProviderFactory, DatabaseProviderFactory>();
        services.AddSingleton<IConnectionManager, ConnectionManager>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IQueryHistoryService, JsonQueryHistoryService>();
        services.AddSingleton<IQuerySnippetService, JsonQuerySnippetService>();
        services.AddSingleton<ConnectionHealthMonitor>();
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
        services.AddSingleton<IConnectionTemplateService, ConnectionTemplateService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IProfileSharingService>(sp => 
            new ProfileSharingService(
                sp.GetRequiredService<IConnectionRepository>(),
                sp.GetRequiredService<IQuerySnippetService>()));
        services.AddSingleton<SnackbarNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<SnackbarNotificationService>());
        services.AddSingleton<ThemeService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<ConnectionDialogViewModel>();
        services.AddTransient<QueryTabViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application shutting down");
        _exceptionHandler.Unregister(this);
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

