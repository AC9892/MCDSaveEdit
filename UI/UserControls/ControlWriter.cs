using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
#nullable enable

namespace MCDSaveEdit.UI
{
    public class ControlWriter : TextWriter
    {
        private TextBox textbox;
        private StringBuilder textBuilder = new StringBuilder();
        private readonly Action<string>? _diagnosticReceived;
        public ControlWriter(TextBox textbox, Action<string>? diagnosticReceived = null)
        {
            this.textbox = textbox;
            _diagnosticReceived = diagnosticReceived;
            textbox.Loaded += (_, __) => updateUI();
        }

        public override void Write(char value)
        {
            textBuilder.Append(value);
            if (value == '\n')
            {
                dispatchDiagnostic(textBuilder.ToString());
                updateUI();
            }
        }

        public override void Write(string value)
        {
            textBuilder.Append(value);
            dispatchDiagnostic(value);
            updateUI();
        }

        public override void Flush()
        {
            base.Flush();
            updateUI();
        }

        private void updateUI()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            if (dispatcher.CheckAccess()) updateTextBox();
            else dispatcher.BeginInvoke(new Action(updateTextBox), DispatcherPriority.Background);
        }

        private void dispatchDiagnostic(string value)
        {
            if (_diagnosticReceived == null || string.IsNullOrWhiteSpace(value)) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) _diagnosticReceived(value);
            else dispatcher.BeginInvoke(new Action(() => _diagnosticReceived(value)), DispatcherPriority.Background);
        }

        private void updateTextBox()
        {
            if (_isDisposed == false && textbox.IsLoaded)
            {
                //Update UI here
                textbox.Text = textBuilder.ToString();
                textbox.ScrollToEnd();
            }
        }

        public override Encoding Encoding {
            get { return Encoding.ASCII; }
        }

        private bool _isDisposed = false;
        protected override void Dispose(bool disposing)
        {
            _isDisposed = true;
            base.Dispose(disposing);
        }
    }

    public static class ThreadUtils
    {
        public static bool IsMainThread()
        {
            return Application.Current != null && Application.Current.Dispatcher.Thread == Thread.CurrentThread;
        }

        public static void ExecuteOnMainThread(this object obj, Action funcToRun)
        {
            if (IsMainThread())
            {
                funcToRun();
            }
            else
            {
                _ = Application.Current?.Dispatcher.Invoke(DispatcherPriority.Render, new ThreadStart(delegate {
                    funcToRun();
                }));
            }
        }

        public static void ExecuteOnMainThreadWithNonnullThis<T>(this T lhs, Action<T> funcToRun) where T : class
        {
            lhs.ExecuteOnMainThreadWithWeakThis(weakThis => {
                if (weakThis.TryGetTarget(out T nonnullThis))
                {
                    funcToRun(nonnullThis);
                }
            });
        }
        public static void ExecuteOnMainThreadWithWeakThis<T>(this T lhs, Action<WeakReference<T>> funcToRun) where T : class
        {
            var weakThis = new WeakReference<T>(lhs);
            if (IsMainThread())
            {
                funcToRun(weakThis);
            }
            else
            {
                _ = Application.Current?.Dispatcher.Invoke(DispatcherPriority.Render, new ThreadStart(delegate {
                    funcToRun(weakThis);
                }));
            }
        }
    }

    public class MultiTextWriter : TextWriter
    {
        private List<TextWriter> _writers;
        public MultiTextWriter(IEnumerable<TextWriter> writers)
        {
            this._writers = writers.ToList();
        }

        public MultiTextWriter(params TextWriter[] writers)
        {
            this._writers = writers.ToList();
        }

        public void addWriter(TextWriter writer)
        {
            lock (_writers)
            {
                _writers.Add(writer);
            }
        }

        public void removeWriter(TextWriter writer)
        {
            lock(_writers)
            {
                _writers.Remove(writer);
            }
        }

        public override void Write(char value)
        {
            lock(_writers)
            {
                foreach (var writer in _writers)
                    writer.Write(value);
            }
        }

        public override void Write(string value)
        {
            lock (_writers)
            {
                foreach (var writer in _writers)
                    writer.Write(value);
            }
        }

        public override void Flush()
        {
            lock (_writers)
            {
                foreach (var writer in _writers)
                    writer.Flush();
            }
        }

        public override void Close()
        {
            lock (_writers)
            {
                foreach (var writer in _writers)
                    writer.Close();
            }
        }

        public override Encoding Encoding {
            get { return Encoding.ASCII; }
        }
    }
}
