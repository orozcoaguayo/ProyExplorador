using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace ProyExplorador.Views
{
    public partial class CalculatorWindow : Window
    {
        private double _valorActual = 0;
        private double? _valorAnterior = null;
        private string? _operador = null;
        private bool _nuevaEntrada = true;
        private double _memoria = 0;
        private readonly LinkedList<string> _historial = new();

        public CalculatorWindow()
        {
            InitializeComponent();
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            TxtDisplay.Text = _valorActual.ToString(CultureInfo.InvariantCulture);
        }

        private void PushHistorial(string texto)
        {
            _historial.AddFirst(texto);
            while (_historial.Count > 50) _historial.RemoveLast();
            LstHistorial.ItemsSource = _historial.ToList();
        }

        private void SetEntrada(string s)
        {
            if (_nuevaEntrada)
            {
                TxtDisplay.Text = s;
                _valorActual = double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
                _nuevaEntrada = false;
            }
            else
            {
                var cur = TxtDisplay.Text;
                if (cur == "0") cur = s;
                else cur += s;
                TxtDisplay.Text = cur;
                _valorActual = double.TryParse(cur, NumberStyles.Any, CultureInfo.InvariantCulture, out var v2) ? v2 : _valorActual;
            }
        }

        private void BtnDigit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button b && b.Content is string s)
            {
                SetEntrada(s);
            }
        }

        private void BtnDecimal_Click(object sender, RoutedEventArgs e)
        {
            var cur = TxtDisplay.Text;
            if (!cur.Contains(",") && !cur.Contains("."))
            {
                TxtDisplay.Text = cur + ".";
                _nuevaEntrada = false;
            }
        }

        private void ApplyPendingOperator()
        {
            if (_operador == null || _valorAnterior == null) return;
            try
            {
                double a = _valorAnterior.Value;
                double b = _valorActual;
                double result = b;
                switch (_operador)
                {
                    case "+": result = a + b; break;
                    case "-": result = a - b; break;
                    case "*": result = a * b; break;
                    case "/": result = b == 0 ? double.NaN : a / b; break;
                }
                var expr = $"{a} {_operador} {b} = {result}";
                PushHistorial(expr);
                _valorActual = result;
                _valorAnterior = null;
                _operador = null;
                _nuevaEntrada = true;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOperator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button b && b.Content is string op)
            {
                // Map symbols to internal operators
                string mapped = op switch
                {
                    "+" => "+",
                    "−" => "-",
                    "×" => "*",
                    "÷" => "/",
                    _ => op
                };

                if (_valorAnterior == null)
                {
                    _valorAnterior = _valorActual;
                    _operador = mapped;
                    _nuevaEntrada = true;
                }
                else
                {
                    ApplyPendingOperator();
                    _valorAnterior = _valorActual;
                    _operador = mapped;
                }
            }
        }

        private void BtnEquals_Click(object sender, RoutedEventArgs e)
        {
            if (_valorAnterior != null && _operador != null)
            {
                ApplyPendingOperator();
            }
        }

        private void BtnC_Click(object sender, RoutedEventArgs e)
        {
            _valorActual = 0;
            _valorAnterior = null;
            _operador = null;
            _nuevaEntrada = true;
            UpdateDisplay();
        }

        private void BtnCE_Click(object sender, RoutedEventArgs e)
        {
            _valorActual = 0;
            _nuevaEntrada = true;
            UpdateDisplay();
        }

        private void BtnSqrt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = Math.Sqrt(_valorActual);
                PushHistorial($"√({_valorActual}) = {result}");
                _valorActual = result;
                _nuevaEntrada = true;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSquare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _valorActual * _valorActual;
                PushHistorial($"({_valorActual})² = {result}");
                _valorActual = result;
                _nuevaEntrada = true;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPercent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Percentage relative to previous value if exists
                if (_valorAnterior != null)
                {
                    var perc = (_valorAnterior.Value * _valorActual) / 100.0;
                    PushHistorial($"{_valorActual}% of {_valorAnterior} = {perc}");
                    _valorActual = perc;
                }
                else
                {
                    _valorActual = _valorActual / 100.0;
                    PushHistorial($"{_valorActual}% = {_valorActual}");
                }
                _nuevaEntrada = true;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMC_Click(object sender, RoutedEventArgs e)
        {
            _memoria = 0;
        }

        private void BtnMR_Click(object sender, RoutedEventArgs e)
        {
            _valorActual = _memoria;
            _nuevaEntrada = true;
            UpdateDisplay();
        }

        private void BtnMPlus_Click(object sender, RoutedEventArgs e)
        {
            _memoria += _valorActual;
        }

        private void BtnMMinus_Click(object sender, RoutedEventArgs e)
        {
            _memoria -= _valorActual;
        }
    }
}
