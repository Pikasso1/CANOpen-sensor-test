using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CANOpen_sensor_test;          // brings CanBus
using ScottPlot;                    // WpfPlot

namespace CANOpen_sensor_test;

public partial class MainWindow : Window
{
    // ── fields ─────────────────────────────────────────
    private readonly uint[] _ring = new uint[5000];   // circular buffer
    private int _head;
    private StreamWriter? _log;
    private CanBus? _bus;
    private SensorCatalogue? _catalogue;
    private DeviceInfo? _currentDevice;   // filled after ping


    // constructor
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    // ── start-up logic ────────────────────────────────
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // path on the network share or fallback to exe folder
            string cataloguePath = @"I:\- Konstruktion\Studiemedhjælper\Sensor test software\sensor-catalogue.json";
            if (!File.Exists(cataloguePath))
                cataloguePath = Path.Combine(AppContext.BaseDirectory,
                                            "sensor-catalogue.json");

            _catalogue = SensorCatalogue.Load(cataloguePath);
            if (_catalogue.Devices.Count == 0)
                throw new InvalidDataException("sensor-catalogue.json had no devices!");

            // ── your CAN setup & ping ─────────────────────────────
            _bus = new CanBus();
            _bus.Open();                        // adapter OK

            uint? devType = await Task.Run(() => _bus.ReadDeviceType());
            if (devType is null)
            {
                MessageBox.Show("Sensor not responding – check wiring.");
                return;
            }

            // ── INSERT THIS BLOCK for catalogue lookup ────────────
            string key = $"0x{devType.Value:X8}";
            if (!_catalogue.Devices.TryGetValue(key, out _currentDevice))
            {
                MessageBox.Show($"Device {key} not found in catalogue.",
                                "Catalogue Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }
            // now we have _currentDevice.Description, .Range_mm, .Step_mm, etc.
            Title = $"{_currentDevice.Description} ({key})";
            // ── end insertion ─────────────────────────────────────

            // CSV log file beside the EXE
            _log = new StreamWriter(
                Path.Combine(AppContext.BaseDirectory,
                             $"log_{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
            _log.WriteLine("UtcIso,RawCounts,Position_mm");

            // subscribe + poll
            _bus.PositionReceived += OnPos;
            _bus.StartPolling(intervalMs: 5);

            // ── graph init (one-time) ────────────────────────────
            int N = _ring.Length;
            double[] ringBuffer = new double[N];
            var sig = Plot.Plot.Add.Signal(ringBuffer);
            Plot.Plot.Axes.SetLimitsX(0, N - 1);
            double maxMm = _ring.Max() * _currentDevice.Step_mm;
            Plot.Plot.Axes.SetLimitsY(0, maxMm * 1.1);
            Plot.Refresh();

            // UI timer updates…
            var uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            uiTimer.Tick += (_, _) =>
            {
                double[] snapshot = RingInChronoOrder();
                Array.Copy(snapshot, ringBuffer, N);
                double yMax = Math.Max(1, _ring.Max() * 1.10);
                Plot.Plot.Axes.SetLimitsY(0, yMax);
                Plot.Refresh();
            };
            uiTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CAN error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Return the ring buffer so oldest is first, newest is last.</summary>
    private double[] RingInChronoOrder()
    {
        int head = _head;                        // snapshot – no race after this
        int len = _ring.Length;

        double[] ordered = new double[len];

        int tailCount = len - head;              // from head → end
        Array.Copy(_ring, head, ordered, 0, tailCount);

        Array.Copy(_ring, 0, ordered, tailCount, head); // from 0 → head-1
        return ordered;
    }



    // ── called on each SDO read ───────────────────────
    private void OnPos(uint raw)
    {
        // 1) compute mm from counts
        double mm = (_currentDevice!.Step_mm) * raw;

        // 2) log both raw & mm
        _log?.WriteLine($"{DateTime.UtcNow:o},{raw},{mm:F2}");

        // 3) store the raw in your ring buffer
        _ring[_head] = raw;
        _head = (_head + 1) % _ring.Length;

        // 4) update the UI with mm
        Dispatcher.Invoke(() =>
        {
            LatestLabel.Text = $"{mm:F2} mm";
        });
    }


    // ── tidy-up ───────────────────────────────────────
    protected override void OnClosed(EventArgs e)
    {
        _bus?.StopPolling();
        _bus?.Dispose();
        _log?.Dispose();
        base.OnClosed(e);
    }
}
