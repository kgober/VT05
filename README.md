This is a VT05 terminal emulator for Windows, written in C#.

The aim of this project is to accurately reproduce the experience of using a real
Digital Equipment Corporation (DEC) VT05 terminal, including the look of the screen.

The VT05 was an early "glass teletype" terminal, and had a display size of 72 columns x 20 rows.
It displayed upper-case only, using the "half ASCII" character set.  The keyboard was capable
of sending the full 128-character ASCII set, but could be switched to half ASCII if desired.

This program requires the .NET Framework, v2.0 or newer.

Controls:

Regular keyboard keys work as expected, including arrow keys.  
VT05 special keys: LF=Insert, RUBOUT=Delete, HOME=Home, LOCK+EOL=Alt+End, LOCK+EOS=Alt+PgDn.  
F5 opens the settings dialog.  
F6 opens the connection dialog.  
F11/F12 adjust screen brightness.

Note that the VT05 keyboard layout is more similar to a Teletype Model 33 than a modern PC.

Settings:

The transmit and receive speed can be adjusted in the Settings dialog.  A real VT05 had a maximum
speed of 2400 bps (with the M7004 High-Speed Interface module installed), but the emulator will also
allow "line speed" which means "as fast as the underlying physical connection can go".  The terminal
speed can be set independently of the speed of the underlying physical connection so that you
can, for example, reproduce ASCII animations that look best at a slower speed (even though you
are actually using a much faster connection such as Ethernet).

The keyboard may be set to "half ASCII" in the Settings dialog as well.

Connections:

The emulator can be connected to a target system using your computer's serial port.  The serial
port settings (baud rate, data bits, parity, and stop bits) are configurable.

The emulator can also be connected to a target system over the network, using telnet or raw TCP.  By
default telnet connections use port 23, but connections to other ports can be specified by adding a
colon and the port number to the host name or IP address, e.g. "host:2023" or "192.168.1.100:2023".
Raw TCP connections don't have a default port, so you must always specify it.

Note: BSD telnet historically suppressed negotiation of Telnet options when connecting to a port
other than the Telnet well-known-port (23).  This implementation of telnet always negotiates options
regardless of the specified port.  Use the raw TCP network connection option to connect without
any Telnet processing (options, IAC doubling, CR mapping, etc.).
