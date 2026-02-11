using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PdfToGCode.Core.Services
{
    public class GrblSender
    {
        private SerialPort _serialPort;
        private bool _isConnected;
        private bool _isPaused;
        private bool _isStopped;
        private TaskCompletionSource<bool> _responseReceived;

        public event Action<string> OnLog;
        public event Action<int, int> OnProgress;
        public event Action<bool> OnConnectionChanged;

        public bool IsConnected => _isConnected;

        public async Task ConnectAsync(string portName, int baudRate)
        {
            if (_serialPort != null && _serialPort.IsOpen)
                Disconnect();

            try
            {
                _serialPort = new SerialPort(portName, baudRate);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                _isConnected = true;

                // Wake up GRBL
                _serialPort.Write("\r\n\r\n");
                await Task.Delay(2000); // Wait for GRBL to initialize
                _serialPort.DiscardInBuffer();

                OnConnectionChanged?.Invoke(true);
                OnLog?.Invoke($"Connected to {portName} @ {baudRate}");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Connection Error: {ex.Message}");
                _isConnected = false;
                throw;
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            _isConnected = false;
            OnConnectionChanged?.Invoke(false);
            OnLog?.Invoke("Disconnected");
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadLine(); // GRBL sends responses ending with \r\n
                // OnLog?.Invoke($"< {data.Trim()}");

                if (data.Contains("ok"))
                {
                    _responseReceived?.TrySetResult(true);
                }
                else if (data.Contains("error"))
                {
                    OnLog?.Invoke($"GRBL ERROR: {data}");
                    _responseReceived?.TrySetResult(false); // Or throw?
                }
            }
            catch
            {
                // Serial read error or timeout
            }
        }

        public async Task SendGCodeAsync(string gcode)
        {
            if (!_isConnected) throw new InvalidOperationException("Not connected");

            _isStopped = false;
            _isPaused = false;

            var lines = gcode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int total = lines.Length;

            for (int i = 0; i < total; i++)
            {
                if (_isStopped)
                {
                    OnLog?.Invoke("Sending Stopped.");
                    return;
                }

                while (_isPaused)
                {
                    await Task.Delay(100);
                    if (_isStopped) return;
                }

                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("("))
                    continue;

                // Send line
                // OnLog?.Invoke($"> {line}");
                _responseReceived = new TaskCompletionSource<bool>();

                _serialPort.WriteLine(line);

                // Wait for 'ok'
                // Timeout logic needed?
                var timeoutTask = Task.Delay(10000); // 10s timeout
                var completedTask = await Task.WhenAny(_responseReceived.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    OnLog?.Invoke($"Timeout waiting for response to: {line}");
                    // Continue or stop? Let's continue for now but log.
                }
                else
                {
                    bool result = await _responseReceived.Task;
                    if (!result)
                    {
                        // Error response logic
                    }
                }

                OnProgress?.Invoke(i + 1, total);
            }

            OnLog?.Invoke("Sending Complete.");
        }

        public void Pause()
        {
            _isPaused = true;
            OnLog?.Invoke("Paused.");
        }

        public void Resume()
        {
            _isPaused = false;
            OnLog?.Invoke("Resumed.");
        }

        public void Stop()
        {
            _isStopped = true;
            _isPaused = false; // Break out of pause loop
            // Soft reset GRBL?
            if (_isConnected && _serialPort.IsOpen)
            {
                _serialPort.Write("\u0018"); // Ctrl-X (Soft Reset)
            }
            OnLog?.Invoke("Stopped.");
        }
    }
}
