using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;
using System.Threading;
using TPCANHandle = System.UInt16;

namespace CANOpen_sensor_test;

public sealed class CanBus : IDisposable
{
    private readonly TPCANHandle _chan = PCANBasic.PCAN_USBBUS1;
    private readonly TPCANBaudrate _baud = TPCANBaudrate.PCAN_BAUD_500K;
    private bool _open;

    public void Open(bool listenOnly = false)
    {
        if (_open) return;
        var sts = PCANBasic.Initialize(_chan, _baud);
        if (sts != TPCANStatus.PCAN_ERROR_OK)
            throw new($"Init failed: {sts}");
        _open = true;

        // Hardware filter: boot-up (0x701) and SDO Rx (0x581)
        uint close = PCANBasic.PCAN_FILTER_CLOSE;
        PCANBasic.SetValue(_chan, TPCANParameter.PCAN_MESSAGE_FILTER,
                           ref close, sizeof(uint));
        PCANBasic.FilterMessages(_chan, 0x580, 0x581, TPCANMode.PCAN_MODE_STANDARD);
        PCANBasic.FilterMessages(_chan, 0x701, 0x701, TPCANMode.PCAN_MODE_STANDARD);

        if (listenOnly)
        {
            uint on = PCANBasic.PCAN_PARAMETER_ON;
            PCANBasic.SetValue(_chan, TPCANParameter.PCAN_LISTEN_ONLY, ref on, sizeof(uint));
        }
    }

    /// <summary>SDO read 0x1000 :00 → returns Device Type or null on timeout.</summary>
    public uint? ReadDeviceType(int timeoutMs = 1000)
    {
        // Build SDO client-&gt;server read request (index 0x1000, sub 0)
        var req = new TPCANMsg
        {
            ID = 0x601,
            LEN = 8,
            MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
            DATA = new byte[8] { 0x40, 0x00, 0x10, 0x00, 0, 0, 0, 0 }
        };
        var sts = PCANBasic.Write(_chan, ref req);
        if (sts != TPCANStatus.PCAN_ERROR_OK)
            throw new($"Write failed: {sts}");

        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            sts = PCANBasic.Read(_chan, out TPCANMsg rx, out _);
            if (sts == TPCANStatus.PCAN_ERROR_QRCVEMPTY) continue;
            if (sts != TPCANStatus.PCAN_ERROR_OK) throw new($"Read {sts}");

            if (rx.ID == 0x581 &&
                rx.LEN == 8 &&
                rx.DATA[0] == 0x43 &&   // expedited, 4-byte resp
                rx.DATA[1] == 0x00 && rx.DATA[2] == 0x10)
            {
                return BitConverter.ToUInt32(rx.DATA, 4); // little-endian
            }
        }
        return null; // timed-out
    }

    public void Dispose()
    {
        if (_open) PCANBasic.Uninitialize(_chan);
        _open = false;
    }
}
