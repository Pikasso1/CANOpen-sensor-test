using System;
using System.Threading.Tasks;
using System.Windows;

namespace CANOpen_sensor_test;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var bus = new CanBus();
            bus.Open();                       // TX enabled, normal mode

            // quick await so UI thread stays responsive
            uint? devType = await Task.Run(() => bus.ReadDeviceType());

            if (devType is uint v)
                MessageBox.Show($"Device Type 0x{v:X8} detected ✔",
                                "CAN-open Ping", MessageBoxButton.OK,
                                MessageBoxImage.Information);
            else
                MessageBox.Show("Sensor did not respond within 1 s ❌",
                                "CAN-open Ping", MessageBoxButton.OK,
                                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }
    }

}
