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

    // CanBus.cs  (add below existing methods)
    public event Action<uint> PositionReceived = delegate { };   // fired on every read
    private CancellationTokenSource? _cts;

    public void StartPolling(int intervalMs = 10)
    {
        if (_cts is not null) throw new InvalidOperationException("Already polling");
        _cts = new();
        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var (ok, val) = ReadPositionOnce();   // SDO 0x6020:01
                if (ok) PositionReceived(val);
                await Task.Delay(intervalMs, _cts.Token);
            }
        }, _cts.Token);
    }

    public void StopPolling()
    {
        _cts?.Cancel();
        _cts = null;
    }

    /// <returns>(true,value) if reply arrived, else (false,0)</returns>
    private (bool ok, uint value) ReadPositionOnce()
    {
        // SDO read idx 0x6020 sub 01
        var req = new TPCANMsg
        {
            ID = 0x601,
            LEN = 8,
            MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
            DATA = new byte[8] { 0x40, 0x20, 0x60, 0x01, 0, 0, 0, 0 }
        };
        PCANBasic.Write(_chan, ref req);

        var deadline = Environment.TickCount64 + 10;               // 10 ms timeout
        while (Environment.TickCount64 < deadline)
        {
            var sts = PCANBasic.Read(_chan, out TPCANMsg rx, out _);
            if (sts == TPCANStatus.PCAN_ERROR_QRCVEMPTY) continue;
            if (sts != TPCANStatus.PCAN_ERROR_OK) break;

            if (rx.ID == 0x581 && rx.DATA[0] == 0x43 &&
                rx.DATA[1] == 0x20 && rx.DATA[2] == 0x60)
            {
                uint val = BitConverter.ToUInt32(rx.DATA, 4);
                // Skip underflow pattern (0xFFFFFxxx)
                if (val >= 0xFFFFFFF0) return (false, 0);
                return (true, val);
            }
        }
        return (false, 0);
    }

}
