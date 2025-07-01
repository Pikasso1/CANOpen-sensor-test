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
            _bus = new CanBus();
            _bus.Open();                        // adapter OK

            uint? devType = await System.Threading.Tasks.Task
                                   .Run(() => _bus.ReadDeviceType());
            if (devType is null)
            {
                MessageBox.Show("Sensor not responding – check wiring.");
                return;
            }
            Title = $"SGH-25  DeviceType 0x{devType:X8}";

            // CSV log file beside the EXE
            _log = new StreamWriter(
                Path.Combine(AppContext.BaseDirectory,
                $"log_{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
            _log.WriteLine("UtcIso,RawCounts");

            // subscribe + poll
            _bus.PositionReceived += OnPos;
            _bus.StartPolling(intervalMs: 5);

            // ── graph init (one-time) ─────────────────
            int N = _ring.Length;
            double[] ringBuffer = new double[N];

            // plot the fixed-length buffer once
            var sig = Plot.Plot.Add.Signal(ringBuffer);
            Plot.Plot.Axes.SetLimitsX(0, N - 1);                // oldest at x=0, newest at x=N-1
            Plot.Plot.Axes.SetLimitsY(0, _ring.Max() * 1.1 + 1);// start with 10% headroom
            Plot.Refresh();

            // UI timer updates graph every 100 ms ──────────
            var uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            uiTimer.Tick += (_, _) =>
            {
                // 1) build a chronologically ordered snapshot
                double[] snapshot = RingInChronoOrder();    // oldest→newest

                // 2) copy it into the plotted array
                Array.Copy(snapshot, ringBuffer, N);

                // 3) autoscale Y to 110% of the current max
                double yMax = Math.Max(1, _ring.Max() * 1.10);
                Plot.Plot.Axes.SetLimitsY(0, yMax);

                // 4) redraw
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
        // head points to next write pos (newest+1). Oldest == head.
        // copy into a new double[] for ScottPlot
        double[] ordered = new double[_ring.Length];
        int idx = 0;

        // copy [head … end)
        for (int i = _head; i < _ring.Length; i++)
            ordered[idx++] = _ring[i];

        // copy [0 … head)
        for (int i = 0; i < _head; i++)
            ordered[idx++] = _ring[i];

        return ordered;
    }


    // ── called on each SDO read ───────────────────────
    private void OnPos(uint raw)
    {
        _log?.WriteLine($"{DateTime.UtcNow:o},{raw}");

        _ring[_head] = raw;                 // ring buffer write
        _head = (_head + 1) % _ring.Length;

        // update big number on UI thread
        Dispatcher.Invoke(() => LatestLabel.Text = raw.ToString());
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
