using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// Base común para todos los ViewModels.
    /// Incluye: IsBusy, Error, CancellationToken, Dispose y métricas de tiempo.
    /// </summary>
    public abstract partial class ViewModelBase : ObservableObject, IDisposable
    {
        [ObservableProperty] private bool   _isBusy;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _errorMessage  = string.Empty;

        // CancellationToken compartido por vista — se cancela al navegar fuera o al Dispose
        private CancellationTokenSource _cts = new();
        protected CancellationToken CancellationToken => _cts.Token;

        private bool _disposed;

        // ── Busy helpers ────────────────────────────────────────────────────
        protected void SetBusy(bool value, string message = "")
        {
            IsBusy        = value;
            StatusMessage = message;
        }

        protected void SetError(string message) => ErrorMessage = message;
        protected void ClearError()             => ErrorMessage = string.Empty;

        // ── Timed operation ─────────────────────────────────────────────────
        /// <summary>Ejecuta <paramref name="work"/> midiendo su duración; emite el tiempo en <paramref name="onComplete"/>.</summary>
        protected static async Task<T> TimedAsync<T>(Func<Task<T>> work, Action<TimeSpan>? onComplete = null)
        {
            var sw = Stopwatch.StartNew();
            try   { return await work(); }
            finally
            {
                sw.Stop();
                onComplete?.Invoke(sw.Elapsed);
            }
        }

        // ── Navigation lifecycle ────────────────────────────────────────────
        /// <summary>Inicialización diferida al mostrar la vista.</summary>
        public virtual Task InitializeAsync() => Task.CompletedTask;

        /// <summary>Llamado cuando el usuario navega fuera. Cancela operaciones pendientes.</summary>
        public virtual void OnNavigatedFrom()
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        // ── IDisposable ─────────────────────────────────────────────────────
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
