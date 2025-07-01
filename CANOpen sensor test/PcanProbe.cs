// PcanProbe.cs
using Peak.Can.Basic;           // brings the namespace in
using Peak.Can.Basic.BackwardCompatibility;
using TPCANHandle = System.UInt16; // alias required for TPCANHandle

namespace CANOpen_sensor_test;

internal static class PcanProbe
{
    internal static void Touch()
    {
        // merely reference a few symbols so the compiler must resolve them
        TPCANHandle handle = PCANBasic.PCAN_USBBUS1;
        var baud = TPCANBaudrate.PCAN_BAUD_500K;
        var status = TPCANStatus.PCAN_ERROR_OK;
        _ = handle; _ = baud; _ = status;
    }
}
