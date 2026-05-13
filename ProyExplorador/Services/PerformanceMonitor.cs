using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio de diagnóstico de rendimiento.
    /// Expone métricas en tiempo real: CPU, memoria y tiempos de operación.
    /// Se puede usar desde cualquier ViewModel para registrar tiempos.
    /// </summary>
    public sealed class PerformanceMonitor : IDisposable
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly System.Timers.Timer _samplingTimer;
        private bool _disposed;

        // Última muestra de memoria del proceso
        public long WorkingSetMB   { get; private set; }
        public long PrivateBytesMB { get; private set; }
        public double CpuPercent   { get; private set; }

        private TimeSpan _lastCpuTime = TimeSpan.Zero;
        private DateTime _lastSample  = DateTime.UtcNow;

        public event EventHandler? MetricsUpdated;

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;

            _samplingTimer = new System.Timers.Timer(5000); // cada 5 s
            _samplingTimer.Elapsed  += (_, _) => Sample();
            _samplingTimer.AutoReset = true;
            _samplingTimer.Start();
        }

        // ── Timed region helper ─────────────────────────────────────────
        /// <summary>
        /// Ejecuta <paramref name="work"/> y emite log con la duración.
        /// Útil para medir tiempos de carga de carpetas, búsquedas, etc.
        /// </summary>
        public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> work)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return await work().ConfigureAwait(false);
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("[Perf] {Operation} completed in {ElapsedMs} ms",
                    operationName, sw.ElapsedMilliseconds);
            }
        }

        public async Task MeasureAsync(string operationName, Func<Task> work)
        {
            var sw = Stopwatch.StartNew();
            try   { await work().ConfigureAwait(false); }
            finally
            {
                sw.Stop();
                _logger.LogInformation("[Perf] {Operation} completed in {ElapsedMs} ms",
                    operationName, sw.ElapsedMilliseconds);
            }
        }

        // ── Sampling ────────────────────────────────────────────────────
        private void Sample()
        {
            try
            {
                _process.Refresh();

                WorkingSetMB   = _process.WorkingSet64 / (1024 * 1024);
                PrivateBytesMB = _process.PrivateMemorySize64 / (1024 * 1024);

                var now     = DateTime.UtcNow;
                var cpu     = _process.TotalProcessorTime;
                var elapsed = (now - _lastSample).TotalMilliseconds;

                if (elapsed > 0)
                    CpuPercent = (cpu - _lastCpuTime).TotalMilliseconds / elapsed /
                                 Environment.ProcessorCount * 100;

                _lastCpuTime = cpu;
                _lastSample  = now;

                _logger.LogDebug("[Perf] WS={WS}MB  Private={Priv}MB  CPU={Cpu:F1}%",
                    WorkingSetMB, PrivateBytesMB, CpuPercent);

                MetricsUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Perf sampling error"); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _samplingTimer.Stop();
            _samplingTimer.Dispose();
            _process.Dispose();
            _disposed = true;
        }
    }
}
