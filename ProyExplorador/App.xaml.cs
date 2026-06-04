using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProyExplorador.Services;
using ProyExplorador.ViewModels;
using ProyExplorador.Parsers;
using ProyExplorador.Views;
using System.Diagnostics;
using System.Windows;

namespace ProyExplorador
{
    /// <summary>
    /// Entry point de la aplicación.
    /// Configura DI, logging, caching y mide tiempo de arranque.
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var startupSw = Stopwatch.StartNew();

            // ── Contenedor de servicios ──────────────────────────────────
            var services = new ServiceCollection();

            // Logging — escribe en Debug Output de Visual Studio
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddDebug();
            });

            // Memory cache para ThumbnailService (máx 256 entradas)
            services.AddMemoryCache(o =>
            {
                o.SizeLimit        = 256;
                o.CompactionPercentage = 0.25;
            });

            // Servicios singleton
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IFileService,       FileService>();
            services.AddSingleton<ISettingsService,   SettingsService>();
            services.AddSingleton<IThumbnailService,  ThumbnailService>();
            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<CleanupService>();

            // ViewModels — registrar Multimedia antes de FileReader/FileExplorer por dependencias
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<MultimediaViewModel>();
            services.AddSingleton<FileReaderViewModel>();
            services.AddSingleton<FileExplorerViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<CleanupViewModel>();
            services.AddSingleton<StatsViewModel>();
            services.AddSingleton<SettingsViewModel>();
            // DataViewer parsers kept if other parts need them
            services.AddSingleton<JsonParser>();
            services.AddSingleton<CsvParser>();
            services.AddSingleton<XmlParser>();
            services.AddSingleton<HtmlParser>();
            services.AddSingleton<MainViewModel>();

            services.AddTransient<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // ── Cargar configuración persistida ─────────────────────────
            _serviceProvider.GetRequiredService<ISettingsService>().Load();

            // ── Startup completado ───────────────────────────────────────
            startupSw.Stop();
            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("ProyExplorador started in {ElapsedMs} ms", startupSw.ElapsedMilliseconds);

            // ── Mostrar ventana principal ────────────────────────────────
            var window = _serviceProvider.GetRequiredService<MainWindow>();
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.GetService<ISettingsService>()?.Save();

            // Dispose de servicios que implementan IDisposable
            _serviceProvider?.GetService<IThumbnailService>()?.Dispose();
            _serviceProvider?.GetService<PerformanceMonitor>()?.Dispose();

            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}

