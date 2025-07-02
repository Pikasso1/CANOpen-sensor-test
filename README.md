This is a semi hobby project to test using PEAKs API for CANOpen software. It was originally built to maximize intuitivity for a construction crew to handle the testing and setup.

Hardware needed:
PEAK PCAN-USB. I used IPEH-002021 with a serial number > 200.000 with 120ohm ternimation activated
CANOpen sensor. I used a SGH25 with external powersupply of 25V. Readings were accurate even though termination wasnt active in sensor.

Current features include:
CAN Bus connection. Which is currently hardcoded to 500kbps in Phase 4. Change TPCANBaudrate Enumerator before build to configure bus speed
Primitive node guarding used to detect status of sensor. Done to circumvent hotswapping because SGH25 only sends heartbeat on startup.

Automatic detection of device type via. CAN 1000h respons from sensor. Relies on bus speed matching the sensors configuration
Automatic lookup of sensor and device specific characteristics. Done through JSON catalouge with step size and physical length to calculate mm/steps

Position shown via. catalouge mm/step conversion
Live updating graph through ScottPlot, with ring buffer to show latest readings. Change buffer size of _ring in MainWindow.xaml.cs to change time frame shown on screen. 
