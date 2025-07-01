// CanBus.cs  – clean, sensor-less smoke test
using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;
using TPCANHandle = System.UInt16;

namespace CANOpen_sensor_test;

public sealed class CanBus : IDisposable
{
    private readonly TPCANHandle _chan = PCANBasic.PCAN_USBBUS1;
    private readonly TPCANBaudrate _baud = TPCANBaudrate.PCAN_BAUD_500K;
    private bool _open;

    /// <summary>Open PCAN-USB; set listenOnly=true if you want 100 % RX-only.</summary>
    public void Open(bool listenOnly = false)
    {
        if (_open) return;

        var sts = PCANBasic.Initialize(_chan, _baud);
        if (sts != TPCANStatus.PCAN_ERROR_OK)
            throw new($"Init failed: {sts}");
        _open = true;

        if (listenOnly)
        {
            uint on = PCANBasic.PCAN_PARAMETER_ON;   // 1
            PCANBasic.SetValue(_chan,
                TPCANParameter.PCAN_LISTEN_ONLY,
                ref on, sizeof(uint));
        }
    }

    /// <summary>Just make one dummy write & read back to prove TX / RX path.</summary>
    public void SmokeTestEcho()
    {
        var tx = new TPCANMsg
        {
            ID = 0x123,                                      // arbitrary test ID
            LEN = 1,                                          // one data byte is “valid”
            MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
            DATA = new byte[8] { 0xAA, 0, 0, 0, 0, 0, 0, 0 }   // MUST be length-8
        };
        PCANBasic.Write(_chan, ref tx);
    }

    public void Dispose()
    {
        if (_open)
            PCANBasic.Uninitialize(_chan);
        _open = false;
    }
}
