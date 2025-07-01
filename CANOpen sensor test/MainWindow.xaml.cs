using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace CANOpen_sensor_test;

public partial class MainWindow
{
    // --- ring-buffer & logging helpers ---
    private readonly uint[] _ring = new uint[500];   // live-display window
    private int _head;
    private StreamWriter? _log;
    private CanBus? _bus;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _bus = new CanBus();
            _bus.Open();                       // adapter + filter
            uint? devType = await Task.Run(() => _bus.ReadDeviceType());  // ping once

            if (devType is null)
            {
                MessageBox.Show("Sensor timed-out – check wiring / power.");
                return;
            }
            Title = $"SGH-25  DeviceType 0x{devType:X8}";

            // --- CSV log file ---
            _log = new StreamWriter($"log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            _log.WriteLine("UtcIso,RawCounts");

            // --- subscribe + start poll ---
            _bus.PositionReceived += OnPos;
            _bus.StartPolling(intervalMs: 5);           // 5 ms loop (~200 Hz)

            // optional: if you added a Label called LatestLabel in XAML
            LatestLabel.Content = "Polling…";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CAN error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void OnPos(uint raw)
    {
        // 1) log to CSV
        _log?.WriteLine($"{DateTime.UtcNow:o},{raw}");

        // 2) store in ring buffer
        _ring[_head] = raw;
        _head = (_head + 1) % _ring.Length;

        // 3) quick UI update – hop to UI thread
        Dispatcher.Invoke(() =>
        {
            LatestLabel.Content = raw;        // numeric read-out
        });
    }
    protected override void OnClosed(EventArgs e)
    {
        _bus?.StopPolling();
        _bus?.Dispose();
        _log?.Dispose();
        base.OnClosed(e);
    }
}
