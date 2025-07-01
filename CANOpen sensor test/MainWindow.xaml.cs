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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var bus = new CanBus();
            bus.Open(listenOnly: true);   // safe, RX-only
            bus.SmokeTestEcho();          // optional dummy TX
            MessageBox.Show("PCAN-USB channel opened OK ✔",
                            "Smoke-test", MessageBoxButton.OK);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CAN error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }
    }
}
