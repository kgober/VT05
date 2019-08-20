// VT05.cs
// Copyright (c) 2016, 2017, 2019 Kenneth Gober
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Emulator
{
    public partial class Terminal
    {
        // VT05 Emulator
        // References:
        // VT05 Alphanumeric Display Terminal Reference Manual [DEC-00-H4AC-D]
        // VT05 Alphanumeric Display Terminal Maintenance Manual, Volume 1 [DEC-00-H4BD-D]

        // Future Improvements / To Do
        // verify bell code thread safety between worker thread and cursor timer thread
        // correct power-on sequencing (including bell at correct time)
        // verify whether bell starts sounding when cursor turns on, or when cursor turns off
        // A/B models (300 baud max / 2400 baud max)
        // correct padding behavior (consult schematics to see what happens for nonzero padding after CAD Y)
        // correct padding time (17ms assumed - "slightly greater than one full cycle of the AC line")
        // integrate padding timeout with UART receive clock (avoid padding misapplication when receiving 'catch up' bursts of chars)
        // contrast control
        // shift lock key
        // correct typeahead behavior (i.e. none)
        // add command line option for serial connection
        // allow re-use of recent network destinations
        // store previous serial port configuration
        // log to file
        // right click context menu? (if so, move Paste option there)


        // Terminal-MainWindow Interface [Main UI Thread]

        public partial class VT05 : Terminal
        {
            public VT05()
            {
                InitKeyboard();
                InitDisplay();
                InitIO();
                PowerOnComplete();
                ParseArgs(Program.Args);
            }

            private void ParseArgs(String[] args)
            {
                Int32 ap = 0;
                while (ap < args.Length)
                {
                    String arg = args[ap++];
                    if ((arg != null) && (arg.Length != 0))
                    {
                        Char c = arg[0];
                        if (((c == '-') || (c == '/')) && (arg.Length > 1))
                        {
                            switch (arg[1])
                            {
                                case 'o':
                                case 'O':
                                    arg = arg.Substring(2);
                                    if ((arg.Length == 0) && (ap < args.Length)) arg = args[ap++];
                                    while (arg.Length != 0)
                                    {
                                        if (arg.StartsWith("r+", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptAutoRepeat = true;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptAutoRepeat = true;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("r-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptAutoRepeat = false;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptAutoRepeat = false;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("h+", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptHalfASCII = true;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptHalfASCII = true;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("h-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptHalfASCII = false;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptHalfASCII = false;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("b+", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptBackspaceIsDEL = true;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptBackspaceSendsDEL = true;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("b-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptBackspaceIsDEL = false;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptBackspaceSendsDEL = false;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("m+", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptMarginBell = true;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptMarginBell = true;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("m-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptMarginBell = false;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptMarginBell = false;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("d+", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptStretchDisplay = true;
                                            Program.Window.FixedAspectRatio = false;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptStretchDisplay = true;
                                            arg = arg.Substring(2);
                                        }
                                        else if (arg.StartsWith("d-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mOptStretchDisplay = false;
                                            Program.Window.FixedAspectRatio = true;
                                            if (dlgSettings == null) dlgSettings = new SettingsDialog();
                                            dlgSettings.OptStretchDisplay = false;
                                            arg = arg.Substring(2);
                                        }
                                    }
                                    break;
                                case 'r':
                                case 'R':
                                    arg = arg.Substring(2);
                                    if ((arg.Length == 0) && (ap < args.Length)) arg = args[ap++];
                                    if (dlgConnection == null) dlgConnection = new ConnectionDialog();
                                    dlgConnection.Set(typeof(IO.RawTCP), arg);
                                    mUART.IO = ConnectRawTCP(dlgConnection.Options);
                                    break;
                                case 't':
                                case 'T':
                                    arg = arg.Substring(2);
                                    if ((arg.Length == 0) && (ap < args.Length)) arg = args[ap++];
                                    if (dlgConnection == null) dlgConnection = new ConnectionDialog();
                                    dlgConnection.Set(typeof(IO.Telnet), arg);
                                    mUART.IO = ConnectTelnet(dlgConnection.Options);
                                    break;
                            }
                        }
                    }
                }
            }
        }


        // Terminal Input (Keyboard & Switches) [Main UI Thread]

        // VT05 Key Mappings:
        //   most ASCII character keys function as labeled on PC
        //   LF = Insert (also Keypad Enter)
        //   CR = Enter
        //   RUB OUT = Delete
        //   ALT = Esc
        //   Up = Up
        //   Down = Down
        //   Right = Right
        //   Left = Left
        //   EOS = PgDn (use with LOCK)
        //   EOL = End (use with LOCK)
        //   HOME = Home
        //   LOCK = Alt
        // Contrast Knob: F11 (decrease) & F12 (increase)

        // Note the following differences between VT05 and PC keyboards:
        //   VT05 Shift-2 is ", PC is @
        //   VT05 Shift-6 is &, PC is ^
        //   VT05 Shift-7 is ', PC is &
        //   VT05 Shift-8 is (, PC is *
        //   VT05 Shift-9 is ), PC is (
        //   VT05 Shift-0 is 0, PC is )
        //   VT05 Shift-- is =, PC is _
        //   VT05 Shift-@ is `
        //   VT05 Shift-^ is ~
        //   VT05 Shift-; is +
        //   VT05 Shift-: is *
        //   VT05 does not have a Backspace key, just RUB OUT and Left
        //   VT05 CTRL modifier clears bits 6 and 7 of the associated key
        //   VT05 SHIFT modifier complements bit 6 if bit 7 is set, or bit 5 if bit 7 isn't set

        // Questions to be answered:
        //   Which of these key combinations send NUL (if any?): CTRL-SPACE, CTRL-@
        //   What is sent when you press SHIFT-SPACE? SPACE (32), 0 (48), or nothing?
        //   What is sent when you press SHIFT-0? 0 (48), SPACE (32), or nothing?
        //   What is sent when you press SHIFT-_? _ (95), DEL (127), or nothing?
        //   What is sent when you press SHIFT-RUBOUT? _ (95), DEL (127), or nothing?
        //   Which of these key combinations send EOS (31) (if any?): CTRL-_, CTRL-RUBOUT, CTRL-SHIFT-/
        //   What is sent when you press CTRL-_? EOS (31)?
        //   What is sent when you press CTRL-RUBOUT? EOS (31)?
        //   Is CTRL-- a usable substitute for CTRL-M or CR?
        //   Is CTRL-: a usable substitute for CTRL-J or LF?


        public partial class VT05
        {
            private List<VK> mKeys;                 // keys currently pressed
            private Boolean mShift;                 // Shift is pressed
            private Boolean mCtrl;                  // Ctrl is pressed
            private Boolean mAlt;                   // Alt is pressed
            private Boolean mCaps;                  // Caps Lock is enabled
            private Boolean mOptAutoRepeat;         // enable automatic key repeat
            private Boolean mOptHalfDuplex;         // transmitter is wired directly to receiver
            private Boolean mOptHalfASCII;          // keyboard sends 96 characters rather than 128
            private Boolean mOptBackspaceIsDEL;     // Backspace key sends DEL rather than BS
            private Boolean mOptMarginBell;         // margin bell enabled
            private Boolean mOptStretchDisplay;     // allow variable aspect ratio
            private SettingsDialog dlgSettings;
            private ConnectionDialog dlgConnection;

            private void InitKeyboard()
            {
                mKeys = new List<VK>();
                mCaps = Console.CapsLock;
                mOptBackspaceIsDEL = true;
                mOptMarginBell = true;
            }

            public override Boolean KeyEvent(Int32 msgId, IntPtr wParam, IntPtr lParam)
            {
                switch (msgId)
                {
                    case 0x0100:    // WM_KEYDOWN
                    case 0x0104:    // WM_SYSKEYDOWN
                        return KeyDown(wParam, lParam);
                    case 0x0101:    // WM_KEYUP
                    case 0x0105:    // WM_SYSKEYUP
                        return KeyUp(wParam, lParam);
                    default:
                        return false;
                }
            }

            // System Menu can cause state of Alt key to be lost (it could be up or down)
            public override Boolean MenuEvent(Int32 msgId, IntPtr wParam, IntPtr lParam)
            {
                Debug.WriteLine("MenuEvent: msgId=0x{0:x4} wParam=0x{1:x4} lParam=0x{2:x8}", msgId, (Int32)wParam, (Int32)lParam);
                if (msgId == 0x0112) // WM_SYSCOMMAND
                {
                    switch ((Int32)wParam)
                    {
                        case 5: // Settings (F5)
                            AskSettings();
                            return true;
                        case 6: // Connection (F6)
                            AskConnection();
                            return true;
                        case 11: // Brightness - (F11)
                            LowerBrightness();
                            return true;
                        case 12: // Brightness + (F12)
                            RaiseBrightness();
                            return true;
                        case 99: // About
                            String v = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                            System.Windows.Forms.MessageBox.Show(String.Concat(Program.Name, " v", v, "\r\nCopyright © Kenneth Gober 2016, 2017, 2019\r\nhttps://github.com/kgober/VT05"), String.Concat("About ", Program.Name));
                            return true;
                        case 0xf100: // System Menu
                            //return true; // prevent System Menu opening to prevent losing track of Alt and Space status 
                        default:
                            return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            public override void Paste(string text)
            {
                foreach (Char c in text)
                {
                    if (c < 128) Input(c);
                }
            }

            private Boolean KeyDown(IntPtr wParam, IntPtr lParam)
            {
                Char c;
                VK k = MapKey(wParam, lParam);
                Int32 l = lParam.ToInt32();
                Debug.WriteLine("KeyDown: wParam={0:X8} lParam={1:X8} vk={2} (0x{3:X2}) num={4}", (Int32)wParam, l, k.ToString(), (Int32)k, Console.NumberLock);

                // auto-repeat always enabled for F11 & F12
                if (k == VK.F11) { LowerBrightness(); return true; }
                if (k == VK.F12) { RaiseBrightness(); return true; }

                if (!mKeys.Contains(k))
                    mKeys.Add(k);
                else if (((l & 0x40000000) != 0) && (mOptAutoRepeat == false))
                    return true;

                if ((k >= VK.A) && (k <= VK.Z))
                {
                    c = (Char)(k - VK.A + ((mShift || mCaps || mOptHalfASCII) ? 'A' : 'a'));
                    Input((mCtrl) ? (Char)(c & 31) : c);
                    return true;
                }
                if ((k >= VK.K0) && (k <= VK.K9))
                {
                    c = (Char)(k - VK.K0 + '0');
                    if (mShift)
                    {
                        switch (c)
                        {
                            case '1': c = '!'; break;
                            case '2': c = '@'; break;
                            case '3': c = '#'; break;
                            case '4': c = '$'; break;
                            case '5': c = '%'; break;
                            case '6': c = '^'; break;
                            case '7': c = '&'; break;
                            case '8': c = '*'; break;
                            case '9': c = '('; break;
                            case '0': c = ')'; break;
                        }
                    }
                    Input((mCtrl) ? (Char)(c & 31) : c);
                    return true;
                }
                if ((k >= VK.NUMPAD0) && (k <= VK.NUMPAD9))
                {
                    c = (Char)(k - VK.NUMPAD0 + '0');
                    Input((mCtrl) ? (Char)(c & 31) : c);
                    return true;
                }

                switch (k)
                {
                    case VK.LSHIFT:
                    case VK.RSHIFT:
                        mShift = true;
                        return true;
                    case VK.LCONTROL:
                    case VK.RCONTROL:
                        mCtrl = true;
                        return true;
                    case VK.LALT:
                    case VK.RALT:
                        mAlt = true;
                        return true;
                    case VK.CAPITAL:
                        mCaps = !mCaps;
                        return true;
                    case VK.SPACE:
                        Input((mCtrl) ? '\x00' : ' ');
                        return true;
                    case VK.RETURN:
                        Input('\r');
                        return true;
                    case VK.INSERT:
                        Input('\n');
                        return true;
                    case VK.BACK:
                        Input(mOptBackspaceIsDEL ? ((mCtrl) ? '\x1F' : '\x7F') : '\b');
                        return true;
                    case VK.TAB:
                        Input('\t');
                        return true;
                    case VK.ESCAPE:
                        Input('\x1B');
                        return true;
                    case VK.DELETE:
                        Input((mCtrl) ? '\x1F' : '\x7F');
                        return true;
                    case VK.COMMA:
                        c = (mShift) ? '<' : ',';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.PERIOD:
                        c = (mShift) ? '>' : '.';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.SLASH:
                        c = (mShift) ? '?' : '/';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.SEMICOLON:
                        c = (mShift) ? ':' : ';';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.QUOTE:
                        c = (mShift) ? '"' : '\'';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.MINUS:
                        c = (mShift) ? '_' : '-';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.EQUAL:
                        c = (mShift) ? '+' : '=';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.TILDE:
                        if (!mOptHalfASCII) c = (mShift) ? '~' : '`';
                        else c = (mShift) ? '^' : '@';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.BACKSLASH:
                        c = (mShift && !mOptHalfASCII) ? '|' : '\\';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.LBRACKET:
                        c = (mShift && !mOptHalfASCII) ? '{' : '[';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.RBRACKET:
                        c = (mShift && !mOptHalfASCII) ? '}' : ']';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.UP:
                        Input('\x1A');
                        return true;
                    case VK.DOWN:
                        Input('\x0B');
                        return true;
                    case VK.RIGHT:
                        Input('\x18');
                        return true;
                    case VK.LEFT:
                        Input('\b');
                        return true;
                    case VK.DIVIDE:
                        c = '/';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.MULTIPLY:
                        c = '*';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.SUBTRACT:
                        c = '-';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.ADD:
                        c = '+';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.ENTER:
                        Input('\n');
                        return true;
                    case VK.DECIMAL:
                        c = '.';
                        Input((mCtrl) ? (Char)(c & 31) : c);
                        return true;
                    case VK.HOME:
                        Input('\x1D');
                        return true;
                    case VK.END:
                        if (mAlt) Input('\x1E');
                        return true;
                    case VK.NEXT:
                        if (mAlt) Input('\x1F');
                        return true;
                    case VK.F5:
                        AskSettings();
                        return true;
                    case VK.F6:
                        AskConnection();
                        return true;
                }
                return false;
            }

            private Boolean KeyUp(IntPtr wParam, IntPtr lParam)
            {
                VK k = MapKey(wParam, lParam);
                Int32 l = (Int32)(lParam.ToInt64() & 0x00000000FFFFFFFF);
                Debug.WriteLine("KeyUp: wParam={0:X8} lParam={1:X8} vk={2} (0x{3:X2}) num={4}", (Int32)wParam, l, k.ToString(), (Int32)k, Console.NumberLock);
                if (mKeys.Contains(k)) mKeys.Remove(k);

                if ((k >= VK.A) && (k <= VK.Z)) return true;
                if ((k >= VK.K0) && (k <= VK.K9)) return true;
                if ((k >= VK.NUMPAD0) && (k <= VK.NUMPAD9)) return true;

                switch (k)
                {
                    case VK.LSHIFT:
                        mShift = mKeys.Contains(VK.RSHIFT);
                        return true;
                    case VK.RSHIFT:
                        mShift = mKeys.Contains(VK.LSHIFT);
                        return true;
                    case VK.LCONTROL:
                        mCtrl = mKeys.Contains(VK.RCONTROL);
                        return true;
                    case VK.RCONTROL:
                        mCtrl = mKeys.Contains(VK.LCONTROL);
                        return true;
                    case VK.LALT:
                        mAlt = mKeys.Contains(VK.RALT);
                        return true;
                    case VK.RALT:
                        mAlt = mKeys.Contains(VK.LALT);
                        return true;
                    case VK.CAPITAL:
                        mCaps = Console.CapsLock;
                        return true;
                    case VK.SPACE:
                    case VK.RETURN:
                    case VK.INSERT:
                    case VK.BACK:
                    case VK.TAB:
                    case VK.ESCAPE:
                    case VK.DELETE:
                    case VK.COMMA:
                    case VK.PERIOD:
                    case VK.SLASH:
                    case VK.SEMICOLON:
                    case VK.QUOTE:
                    case VK.MINUS:
                    case VK.EQUAL:
                    case VK.TILDE:
                    case VK.BACKSLASH:
                    case VK.LBRACKET:
                    case VK.RBRACKET:
                    case VK.UP:
                    case VK.DOWN:
                    case VK.RIGHT:
                    case VK.LEFT:
                    case VK.DIVIDE:
                    case VK.MULTIPLY:
                    case VK.SUBTRACT:
                    case VK.ADD:
                    case VK.ENTER:
                    case VK.DECIMAL:
                    case VK.HOME:
                    case VK.END:
                    case VK.NEXT:
                    case VK.F5:
                    case VK.F6:
                    case VK.F11:
                    case VK.F12:
                        return true;
                }
                return false;
            }

            private VK MapKey(IntPtr wParam, IntPtr lParam)
            {
                VK k = (VK)wParam;
                Int32 l = (Int32)(lParam.ToInt64() & 0x00000000FFFFFFFF);
                switch (k)
                {
                    case VK.SHIFT:
                        return (VK)Win32.MapVirtualKey((UInt32)((l & 0x00FF0000) >> 16), MAPVK.VSC_TO_VK_EX);
                    case VK.CONTROL:
                        return ((l & 0x01000000) == 0) ? VK.LCONTROL : VK.RCONTROL;
                    case VK.ALT:
                        return ((l & 0x01000000) == 0) ? VK.LALT : VK.RALT;
                    case VK.RETURN:
                        return ((l & 0x01000000) == 0) ? VK.RETURN : VK.ENTER;
                    default:
                        return k;
                }
            }

            private void Input(String s)
            {
                if (s == null) return;
                for (Int32 i = 0; i < s.Length; i++) Send((Byte)s[i]);
            }

            private void Input(Char c)
            {
                Send((Byte)c);
            }

            public void AskSettings()
            {
                if (dlgSettings == null) dlgSettings = new SettingsDialog();
                dlgSettings.ShowDialog();
                if (!dlgSettings.OK) return;

                mOptAutoRepeat = dlgSettings.OptAutoRepeat;
                mOptMarginBell = dlgSettings.OptMarginBell;
                mOptHalfDuplex = dlgSettings.OptHalfDuplex;
                mOptHalfASCII = dlgSettings.OptHalfASCII;
                mOptBackspaceIsDEL = dlgSettings.OptBackspaceSendsDEL;
                if (dlgSettings.OptStretchDisplay != mOptStretchDisplay)
                {
                    Program.Window.FixedAspectRatio = !dlgSettings.OptStretchDisplay;
                    mOptStretchDisplay = dlgSettings.OptStretchDisplay;
                }

                Int32 t = dlgSettings.TransmitRate;
                Int32 r = dlgSettings.ReceiveRate;
                if (t == -1) return;
                if (r == -1) return;
                SetTransmitSpeed(t);
                SetReceiveSpeed(r);
                SetTransmitParity(dlgSettings.Parity);
            }

            public void AskConnection()
            {
                if (dlgConnection == null) dlgConnection = new ConnectionDialog();
                dlgConnection.ShowDialog();
                if (!dlgConnection.OK) return;
                if (dlgConnection.IOAdapter == typeof(IO.Loopback))
                {
                    mUART.IO = ConnectLoopback(dlgConnection.Options);
                }
                else if (dlgConnection.IOAdapter == typeof(IO.Serial))
                {
                    mUART.IO = ConnectSerial(dlgConnection.Options);
                }
                else if (dlgConnection.IOAdapter == typeof(IO.Telnet))
                {
                    mUART.IO = ConnectTelnet(dlgConnection.Options);
                }
                else if (dlgConnection.IOAdapter == typeof(IO.RawTCP))
                {
                    mUART.IO = ConnectRawTCP(dlgConnection.Options);
                }
            }

            private IO ConnectLoopback(String options)
            {
                if (mUART.IO is IO.Loopback) return mUART.IO;
                try
                {
                    IO.Loopback X = new IO.Loopback(options);
                    String s = String.Concat(Program.Name, " - ", X.ConnectionString);
                    if (String.Compare(s, mCaption) != 0)
                    {
                        mCaption = s;
                        mCaptionDirty = true;
                    }
                    return X;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                    return mUART.IO;
                }
            }

            private IO ConnectSerial(String options)
            {
                if ((mUART.IO is IO.Serial) && (String.Compare(mUART.IO.Options, options) == 0)) return mUART.IO;
                try
                {
                    IO.Serial X = new IO.Serial(options);
                    String s = String.Concat(Program.Name, " - ", X.ConnectionString);
                    if (String.Compare(s, mCaption) != 0)
                    {
                        mCaption = s;
                        mCaptionDirty = true;
                    }
                    return X;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                    return mUART.IO;
                }
            }

            private IO ConnectTelnet(String options)
            {
                if ((mUART.IO is IO.Telnet) && (String.Compare(mUART.IO.Options, options) == 0)) return mUART.IO;
                try
                {
                    IO.Telnet X = new IO.Telnet(options, mUART.ReceiveSpeed, mUART.TransmitSpeed, Display.COLS, Display.ROWS, "DEC-VT05", "VT05");
                    String s = String.Concat(Program.Name, " - ", X.ConnectionString);
                    if (String.Compare(s, mCaption) != 0)
                    {
                        mCaption = s;
                        mCaptionDirty = true;
                    }
                    return X;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                    return mUART.IO;
                }
            }

            private IO ConnectRawTCP(String options)
            {
                if ((mUART.IO is IO.RawTCP) && (String.Compare(mUART.IO.Options, options) == 0)) return mUART.IO;
                try
                {
                    IO.RawTCP X = new IO.RawTCP(options);
                    String s = String.Concat(Program.Name, " - ", X.ConnectionString);
                    if (String.Compare(s, mCaption) != 0)
                    {
                        mCaption = s;
                        mCaptionDirty = true;
                    }
                    return X;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                    return mUART.IO;
                }
            }
        }


        // Terminal Output (Display & Bell)

        public partial class VT05
        {
            private Display mDisplay;
            private Int32 mCAD;                 // processing state for CAD sequences
            private TimeSpan mPadDelay;
            private DateTime mPadTime;
            private Boolean mZeroPad;

            // called by main UI thread via constructor
            private void InitDisplay()
            {
                mDisplay = new Display(this);
                mCAD = 0;
                mPadDelay = new TimeSpan(0, 0, 0, 0, 17);   // 16.667 ms (1/60)
                mPadTime = DateTime.MinValue;
            }

            // called by main UI thread via constructor
            private void PowerOnComplete()
            {
                mDisplay.PowerOnBeep();
            }

            // called by main UI thread
            public override Bitmap Bitmap
            {
                get { return mDisplay.Bitmap; }
            }

            // called by main UI thread
            public override Boolean BitmapDirty
            {
                get { return mDisplay.BitmapDirty; }
                set { mDisplay.BitmapDirty = value; }
            }

            // called by main UI thread via KeyDown() or system menu
            public void LowerBrightness()
            {
                mDisplay.ChangeBrightness(-5);
            }

            // called by main UI thread via KeyDown() or system menu
            public void RaiseBrightness()
            {
                mDisplay.ChangeBrightness(5);
            }

            // called by worker thread
            private void Recv(Byte c)
            {
                Debug.WriteLine("Recv: {0} ({1:D0}/0x{1:X2})", (Char)c, c);
                if (DateTime.Now < mPadTime)
                {
                    Debug.WriteLine("Recv: Padding char dropped");
                    if (mZeroPad && (c != 0))
                    {
                        // CAD malfunction here, but unclear what
                    }
                    return;
                }

                Int32 nx, ny;
                if ((c >= 32) && (c < 127))
                {
                    if (c >= 96) c -= 32;
                    switch (mCAD)
                    {
                        case 0: // regular ASCII characters
                            mDisplay.Char = (Byte)(c - 32);
                            mDisplay.MoveCursorRel(1, 0);
                            return;
                        case 1: // CAD YAD
                            if ((c >= 32) && (c <= 51))
                            {
                                mCAD = c;
                                if (mUART.ReceiveSpeed > 300)
                                {
                                    mPadTime = DateTime.Now + mPadDelay;
                                    mZeroPad = true;
                                }
                            }
                            return;
                        default: // CAD YAD XAD
                            if ((c < 32) || (c > 103)) return;
                            nx = c - 32;
                            ny = mCAD - 32;
                            mCAD = 0;
                            mDisplay.MoveCursorAbs(nx, ny);
                            return;
                    }
                }

                switch ((Char)c)
                {
                    case '\r': // CR - Carriage Return
                        mDisplay.MoveCursorAbs(0, mDisplay.CursorY);
                        return;
                    case '\n': // LF - Line Feed
                        ny = mDisplay.CursorY + 1;
                        if (ny >= Display.ROWS)
                        {
                            ScrollUp();
                            ny = Display.ROWS - 1;
                        }
                        mDisplay.MoveCursorAbs(mDisplay.CursorX, ny);
                        if (mUART.ReceiveSpeed > 300)
                        {
                            mPadTime = DateTime.Now + mPadDelay;
                            mZeroPad = false;
                        }
                        return;
                    case '\b': // BS - Backspace
                        mDisplay.MoveCursorRel(-1, 0);
                        return;
                    case '\t': // HT - Horizontal Tab
                        if (mDisplay.CursorX >= 64)
                            mDisplay.MoveCursorRel(1, 0);
                        else
                            mDisplay.MoveCursorRel(8 - (mDisplay.CursorX % 8), 0);
                        return;
                    case '\a': // BEL - Ring the Bell
                        mDisplay.Beep();
                        return;
                    case '\x0E': // CAD - Direct Cursor Addressing
                        mCAD = 1;
                        return;
                    case '\x1A': // Cursor Up
                        mDisplay.MoveCursorRel(0, -1);
                        if (mUART.ReceiveSpeed > 300)
                        {
                            mPadTime = DateTime.Now + mPadDelay;
                            mZeroPad = false;
                        }
                        return;
                    case '\x0B': // Cursor Down
                        mDisplay.MoveCursorRel(0, 1);
                        if (mUART.ReceiveSpeed > 300)
                        {
                            mPadTime = DateTime.Now + mPadDelay;
                            mZeroPad = false;
                        }
                        return;
                    case '\x18': // Cursor Right
                        mDisplay.MoveCursorRel(1, 0);
                        return;
                    case '\x1D': // Cursor Home
                        mDisplay.MoveCursorAbs(0, 0);
                        if (mUART.ReceiveSpeed > 300)
                        {
                            mPadTime = DateTime.Now + mPadDelay;
                            mZeroPad = false;
                        }
                        return;
                    case '\x1E': // Erase to End-of-Line
                        for (Int32 x = mDisplay.CursorX; x < Display.COLS; x++) mDisplay[x, mDisplay.CursorY] = 0;
                        return;
                    case '\x1F': // Erase to End-of-Screen
                        for (Int32 x = mDisplay.CursorX; x < Display.COLS; x++) mDisplay[x, mDisplay.CursorY] = 0;
                        for (Int32 y = mDisplay.CursorY + 1; y < Display.ROWS; y++)
                        {
                            for (Int32 x = 0; x < Display.COLS; x++) mDisplay[x, y] = 0;
                        }
                        if (mUART.ReceiveSpeed > 300)
                        {
                            mPadTime = DateTime.Now + mPadDelay;
                            mZeroPad = false;
                        }
                        return;
                }
            }

            private void ScrollUp()
            {
                for (Int32 y = 0; y < Display.ROWS - 1; y++)
                {
                    for (Int32 x = 0; x < Display.COLS; x++)
                    {
                        mDisplay[x, y] = mDisplay[x, y + 1];
                    }
                }
                for (Int32 x = 0; x < Display.COLS; x++) mDisplay[x, Display.ROWS - 1] = 0;
            }

            // 72x20 character cells, each cell is 10 raster lines tall and 6 dots wide
            // top raster line is blank, next 7 are for character, next is blank, last is cursor line
            // To simulate the raster, dots are drawn as 2x2 pixels, with a 1 pixel gap below
            // P4 phosphor (white)

            // terminal bell is also handled here, due to relationship between bell and cursor logic.
            // BELL I flip-flop activates bell on positive edge of CHAR 65 BELL (via clock input) or
            // on BELL and STROBE both being high (via preset input).  BELL II flip-flop ensures bell
            // sounds for an entire on/off cycle of the cursor.

            private class Display
            {
                public const Int32 ROWS = 20;
                public const Int32 COLS = 72;
                private const Int32 PIXELS_PER_ROW = 30;
                private const Int32 PIXELS_PER_COL = 12;

                private VT05 mVT05;                     // for calling parent's methods
                private UInt32[] mPixMap;               // pixels
                private GCHandle mPixMapHandle;         // handle for pinned pixels
                private Bitmap mBitmap;                 // bitmap interface
                private volatile Boolean mBitmapDirty;  // true if bitmap has changed
                private Byte[] mChars;              // characters on screen
                private Int32 mX, mY;               // cursor position
                private Timer mCursorTimer;         // cursor blink timer
                private Boolean mCursorVisible;     // whether cursor is currently visible
                private Int32 mBrightness;          // brightness (0-100)
                private UInt32 mOffColor;           // pixel 'off' color
                private UInt32 mOnColor;            // pixel 'on' color
                private SoundPlayer mBell;          // bell sound
                private volatile Boolean mBeepReq;  // whether a beep has been requested
                private Boolean mBeeping;           // whether a beep is currently sounding

                public Display(VT05 parent)
                {
                    mVT05 = parent;
                    Int32 x = COLS * PIXELS_PER_COL;
                    Int32 y = ROWS * PIXELS_PER_ROW;
                    mPixMap = new UInt32[x * y];
                    mPixMapHandle = GCHandle.Alloc(mPixMap, GCHandleType.Pinned);
                    mBitmap = new Bitmap(x, y, x * sizeof(Int32), PixelFormat.Format32bppPArgb, mPixMapHandle.AddrOfPinnedObject());
                    mBitmapDirty = true;
                    mChars = new Byte[COLS * ROWS];
                    mBrightness = 85;   // 85% is the maximum brightness without blue being oversaturated
                    mOffColor = Color(0);
                    mOnColor = Color(mBrightness);
                    mBell = BellSound();
                    mBell.Load();
                    mCursorVisible = false;
                    mCursorTimer = new Timer(CursorTimer_Callback, this, 0, 133); // 7.5 transitions per second (3.75 Hz blink rate)
                }

                public Bitmap Bitmap
                {
                    get { return mBitmap; }
                }

                public Boolean BitmapDirty
                {
                    get { return mBitmapDirty; }
                    set { mBitmapDirty = value; }
                }

                public Int32 CursorX
                {
                    get { return mX; }
                }

                public Int32 CursorY
                {
                    get { return mY; }
                }

                public Byte Char
                {
                    get { return this[mX, mY]; }
                    set { this[mX, mY] = value; }
                }

                public Byte this[Int32 x, Int32 y]
                {
                    get
                    {
                        if ((x < 0) || (x >= COLS)) throw new ArgumentOutOfRangeException("x");
                        if ((y < 0) || (y >= ROWS)) throw new ArgumentOutOfRangeException("y");
                        return mChars[y * COLS + x];
                    }
                    set
                    {
                        if ((x < 0) || (x >= COLS)) throw new ArgumentOutOfRangeException("x");
                        if ((y < 0) || (y >= ROWS)) throw new ArgumentOutOfRangeException("y");
                        Int32 p = y * COLS + x;
                        if (mChars[p] == value) return;
                        mChars[p] = value;
                        p = value * 7;
                        if (p >= CharGen.Length) return;
                        lock (mBitmap)
                        {
                            x *= PIXELS_PER_COL;
                            y *= PIXELS_PER_ROW;
                            Int32 q = y * COLS * PIXELS_PER_COL + x + 2; // +2 to skip first column (2 pixels per dot)
                            for (Int32 dy = 0; dy < 7; dy++)
                            {
                                Byte b = CharGen[p++];
                                // draw first row of pixels (upper half of raster line)
                                Byte m = 16;
                                for (Int32 dx = 0; dx < 10; )
                                {
                                    UInt32 n = ((b & m) == 0) ? mOffColor : mOnColor;
                                    mPixMap[q + dx++] = n;
                                    mPixMap[q + dx++] = n;
                                    m >>= 1;
                                }
                                // advance to next pixel row
                                q += COLS * PIXELS_PER_COL;
                                // draw second row of pixels (lower half of raster line)
                                m = 16;
                                for (Int32 dx = 0; dx < 10; )
                                {
                                    UInt32 n = ((b & m) == 0) ? mOffColor : mOnColor;
                                    mPixMap[q + dx++] = n;
                                    mPixMap[q + dx++] = n;
                                    m >>= 1;
                                }
                                // advance two pixel rows (skip a row for inter-raster-line gap)
                                q += COLS * PIXELS_PER_COL * 2;
                            }
                            mBitmapDirty = true;
                        }
                    }
                }

                public void MoveCursorRel(Int32 dx, Int32 dy)
                {
                    Int32 x = mX + dx;
                    if (x < 0) x = 0; else if (x >= COLS) x = COLS - 1;
                    Int32 y = mY + dy;
                    if (y < 0) y = 0; else if (y >= ROWS) y = ROWS - 1;
                    if ((x >= 64) && (mX < 64) && (mVT05.mOptMarginBell)) Beep();  // margin beep is triggered when cursor enters column 65-72 range
                    if ((x != mX) || (y != mY)) MoveCursorAbs(x, y);
                }

                public void MoveCursorAbs(Int32 x, Int32 y)
                {
                    if ((x < 0) || (x >= COLS)) throw new ArgumentOutOfRangeException("x");
                    if ((y < 0) || (y >= ROWS)) throw new ArgumentOutOfRangeException("y");
                    lock (mBitmap)
                    {
                        if (mCursorVisible)
                        {
                            DrawCursor(mOffColor);
                        }
                        mX = x;
                        mY = y;
                        if (mCursorVisible)
                        {
                            DrawCursor(mOnColor);
                            mBitmapDirty = true;
                        }
                    }
                }

                // this needs to be rewritten to preserve the desired tint even when brightening from zero
                public void ChangeBrightness(Int32 delta)
                {
                    mBrightness += delta;
                    if (mBrightness < 5) mBrightness = 5;
                    else if (mBrightness > 100) mBrightness = 100;
                    UInt32 old = mOnColor;
                    mOnColor = Color(mBrightness);
                    ReplacePixels(old, mOnColor);
                }

                public void PowerOnBeep()
                {
                    while (!mBell.IsLoadCompleted) Thread.Sleep(0);
                    mBeepReq = true;
                    mBell.PlayLooping();
                    mBeeping = true;
                }

                public void Beep()
                {
                    while (!mBell.IsLoadCompleted) Thread.Sleep(0);
                    mBeepReq = true;
                }

                // Construct a WAV file for a 780 Hz square wave recorded at a sample rate of 44.1 kHz, 16 bits per sample, mono.
                // 44100 Hz / 780 Hz = 56.538 samples per wave period, or exactly 735 samples over 13 wave periods
                private SoundPlayer BellSound()
                {
                    Byte[] buf = new Byte[2048]; // only 1514 bytes actually required
                    Int32 p = 0;
                    p += BufWrite(buf, p, "RIFF");
                    p += 4;
                    p += BufWrite(buf, p, "WAVE");
                    p += BufWrite(buf, p, "fmt ");
                    p += BufWrite(buf, p, 16); // 16 bytes follow
                    p += BufWrite(buf, p, (Int16)1); // 1 = PCM
                    p += BufWrite(buf, p, (Int16)1); // 1 = Mono
                    p += BufWrite(buf, p, 44100); // Sample Rate
                    p += BufWrite(buf, p, 88200); // Byte Rate
                    p += BufWrite(buf, p, (Int16)2); // bytes per sample period
                    p += BufWrite(buf, p, (Int16)16); // bits per sample
                    p += BufWrite(buf, p, "data");
                    p += BufWrite(buf, p, 1470); // 735 samples follow at 2 bytes per sample
                    for (Int32 i = 0; i < 13; i++)
                    {
                        for (Int32 j = 0; j < 28; j++) p += BufWrite(buf, p, (Int16)400);
                        for (Int32 j = 0; j < 28; j++) p += BufWrite(buf, p, (Int16)(-400));
                        if ((i % 2) == 0) p += BufWrite(buf, p, (Int16)(-400));
                    }
                    BufWrite(buf, 4, p - 8);
                    return new SoundPlayer(new System.IO.MemoryStream(buf));
                }

                private Int32 BufWrite(Byte[] buffer, Int32 index, String data)
                {
                    return Encoding.ASCII.GetBytes(data, 0, data.Length, buffer, index);
                }

                private Int32 BufWrite(Byte[] buffer, Int32 index, Byte data)
                {
                    buffer[index] = data;
                    return 1;
                }

                private Int32 BufWrite(Byte[] buffer, Int32 index, Int16 data)
                {
                    for (Int32 i = 0; i < 2; i++)
                    {
                        buffer[index++] = (Byte)(data & 0x00FF);
                        data >>= 8;
                    }
                    return 2;
                }

                private Int32 BufWrite(Byte[] buffer, Int32 index, Int32 data)
                {
                    for (Int32 i = 0; i < 4; i++)
                    {
                        buffer[index++] = (Byte)(data & 0x000000FF);
                        data >>= 8;
                    }
                    return 4;
                }

                // P4 phosphor colors (CIE chromaticity coordinates: x=0.275 y=0.290)
                private UInt32 Color(Int32 brightness)
                {
                    if ((brightness < 0) || (brightness > 100)) throw new ArgumentOutOfRangeException("brightness");
                    switch (brightness)
                    {
                        case 100: return 0xFFE6FFFF;
                        case 95: return 0xFFDAF5FF;
                        case 90: return 0xFFCFE8FF;
                        case 85: return 0xFFC4DCFF;
                        case 80: return 0xFFB8CFF1;
                        case 75: return 0xFFADC2E2;
                        case 70: return 0xFFA2B6D3;
                        case 65: return 0xFF96A9C5;
                        case 60: return 0xFF8A9CB6;
                        case 55: return 0xFF7F8FA7;
                        case 50: return 0xFF738298;
                        case 45: return 0xFF677488;
                        case 40: return 0xFF5B6779;
                        case 35: return 0xFF4F5A69;
                        case 30: return 0xFF434C5A;
                        case 25: return 0xFF363E4A;
                        case 20: return 0xFF2A3039;
                        case 15: return 0xFF1D2229;
                        case 10: return 0xFF0f1318;
                        case 5: return 0xFF040506;
                        case 0: return 0xFF000000;
                        default: return 0;
                    }
                }

                private void ReplacePixels(UInt32 oldColor, UInt32 newColor)
                {
                    if (oldColor == newColor) return;
                    lock (mBitmap)
                    {
                        for (Int32 i = 0; i < mPixMap.Length; i++) if (mPixMap[i] == oldColor) mPixMap[i] = newColor;
                        mBitmapDirty = true;
                    }
                }

                private void CursorTimer_Callback(Object state)
                {
                    lock (mBitmap)
                    {
                        mCursorVisible = !mCursorVisible;
                        DrawCursor(mCursorVisible ? mOnColor : mOffColor);
                        mBitmapDirty = true;
                    }

                    if (mCursorVisible)
                    {
                        lock (mBell)
                        {
                            if (mBeepReq)
                            {
                                mBeepReq = false;
                                if (!mBeeping)
                                {
                                    mBeeping = true;
                                    mBell.PlayLooping();
                                }
                            }
                            else if (mBeeping)
                            {
                                mBeeping = false;
                                mBell.Stop();
                            }
                        }
                    }
                }

                private void DrawCursor(UInt32 color)
                {
                    Int32 x = mX * PIXELS_PER_COL;
                    Int32 y = mY * PIXELS_PER_ROW + 24;
                    Int32 p = y * COLS * PIXELS_PER_COL + x;
                    for (Int32 dx = 0; dx < 12; dx++) mPixMap[p + dx] = color;
                    p += COLS * PIXELS_PER_COL;
                    for (Int32 dx = 0; dx < 12; dx++) mPixMap[p + dx] = color;
                }

                // VT05 Font
                static private readonly Byte[] CharGen = {
                    0x00, // _____ space
                    0x00, // _____ 32
                    0x00, // _____ 0x20
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    
                    0x04, // __#__ ! (verified from 2019-05-04 photo)
                    0x04, // __#__ 33
                    0x04, // __#__ 0x21
                    0x04, // __#__
                    0x04, // __#__
                    0x00, // _____
                    0x04, // __#__
                    
                    0x0a, // _#_#_ " (verified from 2019-05-04 photo)
                    0x0a, // _#_#_ 34
                    0x0a, // _#_#_ 0x22
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    
                    0x0a, // _#_#_ # (verified from 2019-05-04 photo)
                    0x0a, // _#_#_ 35
                    0x1f, // ##### 0x23
                    0x0a, // _#_#_
                    0x1f, // #####
                    0x0a, // _#_#_
                    0x0a, // _#_#_
                    
                    0x04, // __#__ $ (verified from 2019-05-04 photo)
                    0x0f, // _#### 36
                    0x14, // #_#__ 0x24
                    0x0e, // _###_
                    0x05, // __#_#
                    0x1e, // ####_
                    0x04, // __#__
                    
                    0x19, // ##__# % (verified from 2019-05-04 photo)
                    0x19, // ##__# 37
                    0x02, // ___#_ 0x25
                    0x04, // __#__
                    0x08, // _#___
                    0x13, // #__##
                    0x13, // #__##
                    
                    0x04, // __#__ & (verified from 2019-05-04 photo)
                    0x0a, // _#_#_ 38
                    0x0a, // _#_#_ 0x26
                    0x0c, // _##__
                    0x15, // #_#_#
                    0x12, // #__#_
                    0x0d, // _##_#
                    
                    0x04, // __#__ ' (verified from 2019-05-04 photo)
                    0x08, // _#___ 39
                    0x10, // #____ 0x27
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    
                    0x02, // ___#_ ( (verified from 2019-05-04 photo)
                    0x04, // __#__ 40
                    0x08, // _#___ 0x28
                    0x08, // _#___
                    0x08, // _#___
                    0x04, // __#__
                    0x02, // ___#_
                    
                    0x08, // _#___ ) (verified from 2019-05-04 photo)
                    0x04, // __#__ 41
                    0x02, // ___#_ 0x29
                    0x02, // ___#_
                    0x02, // ___#_
                    0x04, // __#__
                    0x08, // _#___
                    
                    0x00, // _____ * (verified from 2019-05-04 photo)
                    0x04, // __#__ 42
                    0x15, // #_#_# 0x2A
                    0x0e, // _###_
                    0x15, // #_#_#
                    0x04, // __#__
                    0x00, // _____
                    
                    0x00, // _____ + (verified from 2019-05-04 photo)
                    0x04, // __#__ 43
                    0x04, // __#__ 0x2B
                    0x1f, // #####
                    0x04, // __#__
                    0x04, // __#__
                    0x00, // _____
                    
                    0x00, // _____ , (verified from 2012-04-15 photo)
                    0x00, // _____ 44
                    0x00, // _____ 0x2C
                    0x00, // _____
                    0x04, // __#__
                    0x04, // __#__
                    0x08, // _#___
                    
                    0x00, // _____ - (verified from 2012-04-15 photo)
                    0x00, // _____ 45
                    0x00, // _____ 0x2D
                    0x1f, // #####
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    
                    0x00, // _____ . (verified from 2019-05-04 photo)
                    0x00, // _____ 46
                    0x00, // _____ 0x2E
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x08, // _#___
                    
                    0x01, // ____# / (verified from 2019-05-04 photo)
                    0x01, // ____# 47
                    0x02, // ___#_ 0x2F
                    0x04, // __#__
                    0x08, // _#___
                    0x10, // #____
                    0x10, // #____
                    
                    0x0e, // _###_ 0 (verified from 2012-04-15 photo)
                    0x11, // #___# 48
                    0x13, // #__## 0x30
                    0x15, // #_#_#
                    0x19, // ##__#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x04, // __#__ 1 (verified from 2012-04-15 photo)
                    0x0c, // _##__ 49
                    0x04, // __#__ 0x31
                    0x04, // __#__
                    0x04, // __#__
                    0x04, // __#__
                    0x0e, // _###_
                    
                    0x0e, // _###_ 2 (verified from 2012-04-15 photo)
                    0x11, // #___# 50
                    0x01, // ____# 0x32
                    0x0e, // _###_
                    0x10, // #____
                    0x10, // #____
                    0x1f, // #####
                    
                    0x0e, // _###_ 3 (verified from 2019-05-04 photo)
                    0x11, // #___# 51
                    0x01, // ____# 0x33
                    0x06, // __##_
                    0x01, // ____#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x02, // ___#_ 4 (verified from 2019-05-04 photo)
                    0x06, // __##_ 52
                    0x0a, // _#_#_ 0x34
                    0x12, // #__#_
                    0x1f, // #####
                    0x02, // ___#_
                    0x02, // ___#_
                    
                    0x1f, // ##### 5 (verified from 2012-04-15 photo)
                    0x10, // #____ 53
                    0x1e, // ####_ 0x35
                    0x01, // ____#
                    0x01, // ____#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x06, // __##_ 6 (verified from 2019-05-04 photo)
                    0x08, // _#___ 54
                    0x10, // #____ 0x36
                    0x1e, // ####_
                    0x11, // #___#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x1f, // ##### 7 (verified from 2019-05-04 photo)
                    0x01, // ____# 55
                    0x02, // ___#_ 0x37
                    0x04, // __#__
                    0x08, // _#___
                    0x08, // _#___
                    0x08, // _#___
                    
                    0x0e, // _###_ 8 (verified from 2019-05-04 photo)
                    0x11, // #___# 56
                    0x11, // #___# 0x38
                    0x0e, // _###_
                    0x11, // #___#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x0e, // _###_ 9 (verified from 2019-05-04 photo)
                    0x11, // #___# 57
                    0x11, // #___# 0x39
                    0x0f, // _####
                    0x01, // ____#
                    0x02, // ___#_
                    0x0c, // _##__
                    
                    0x00, // _____ : (verified from 2019-05-04 photo)
                    0x00, // _____ 58
                    0x04, // __#__ 0x3A
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x04, // __#__
                    
                    0x00, // _____ ; (verified from 2019-05-04 photo)
                    0x00, // _____ 59
                    0x04, // __#__ 0x3B
                    0x00, // _____
                    0x04, // __#__
                    0x04, // __#__
                    0x08, // _#___
                    
                    0x01, // ____# < (verified from 2019-05-04 photo)
                    0x02, // ___#_ 60
                    0x04, // __#__ 0x3C
                    0x08, // _#___
                    0x04, // __#__
                    0x02, // ___#_
                    0x01, // ____#
                    
                    0x00, // _____ = (verified from 2019-05-04 photo)
                    0x00, // _____ 61
                    0x1f, // ##### 0x3D
                    0x00, // _____
                    0x1f, // #####
                    0x00, // _____
                    0x00, // _____
                    
                    0x10, // #____ > (verified from 2019-05-04 photo)
                    0x08, // _#___ 62
                    0x04, // __#__ 0x3E
                    0x02, // ___#_
                    0x04, // __#__
                    0x08, // _#___
                    0x10, // #____
                    
                    0x0e, // _###_ ? (verified from 2012-04-15 photo)
                    0x11, // #___# 63
                    0x01, // ____# 0x3F
                    0x02, // ___#_
                    0x04, // __#__
                    0x00, // _____
                    0x04, // __#__
                    
                    0x0e, // _###_ @ (verified from 2019-05-04 photo)
                    0x11, // #___# 64
                    0x17, // #_### 0x40
                    0x15, // #_#_#
                    0x17, // #_###
                    0x10, // #____
                    0x0e, // _###_
                    
                    0x0e, // _###_ A (verified from 2012-04-15 photo, matches maint. man. fig. 3-6)
                    0x11, // #___# 65
                    0x11, // #___# 0x41
                    0x1f, // #####
                    0x11, // #___#
                    0x11, // #___#
                    0x11, // #___#

                    0x1e, // ####_ B (verified from 2012-04-15 photo)
                    0x11, // #___# 66
                    0x11, // #___# 0x42
                    0x1e, // ####_
                    0x11, // #___#
                    0x11, // #___#
                    0x1e, // ####_

                    0x0e, // _###_ C (verified from 2019-05-04 photo, matches maint. man. fig. 3-6)
                    0x11, // #___# 67
                    0x10, // #____ 0x43
                    0x10, // #____
                    0x10, // #____
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x1c, // ###__ D (verified from 2019-05-04 photo)
                    0x12, // #__#_ 68
                    0x11, // #___# 0x44
                    0x11, // #___#
                    0x11, // #___#
                    0x12, // #__#_
                    0x1c, // ###__
                    
                    0x1f, // ##### E (verified from 2019-05-04 photo)
                    0x10, // #____ 69
                    0x10, // #____ 0x45
                    0x1e, // ####_
                    0x10, // #____
                    0x10, // #____
                    0x1f, // #####
                    
                    0x1f, // ##### F (verified from 2012-04-15 photo)
                    0x10, // #____ 70
                    0x10, // #____ 0x46
                    0x1e, // ####_
                    0x10, // #____
                    0x10, // #____
                    0x10, // #____

                    0x0e, // _###_ G (likely, based on 2019-05-04 photo)
                    0x11, // #___# 71
                    0x10, // #____ 0x47
                    0x13, // #__##
                    0x11, // #___#
                    0x11, // #___#
                    0x0f, // _####
                    
                    0x11, // #___# H (verified from 2012-04-15 photo)
                    0x11, // #___# 72
                    0x11, // #___# 0x48
                    0x1f, // #####
                    0x11, // #___#
                    0x11, // #___#
                    0x11, // #___#

                    0x0e, // _###_ I (verified from 2012-04-15 photo)
                    0x04, // __#__ 73
                    0x04, // __#__ 0x49
                    0x04, // __#__
                    0x04, // __#__
                    0x04, // __#__
                    0x0e, // _###_

                    0x01, // ____# J (verified from 2019-05-04 photo)
                    0x01, // ____# 74
                    0x01, // ____# 0x4A
                    0x01, // ____#
                    0x01, // ____#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x11, // #___# K (verified from 2012-04-15 photo)
                    0x12, // #__#_ 75
                    0x14, // #_#__ 0x4B
                    0x18, // ##___
                    0x14, // #_#__
                    0x12, // #__#_
                    0x11, // #___#

                    0x10, // #____ L (verified from 2012-04-15 photo)
                    0x10, // #____ 76
                    0x10, // #____ 0x4C
                    0x10, // #____
                    0x10, // #____
                    0x10, // #____
                    0x1f, // #####

                    0x11, // #___#  (verified from 2019-05-04 photo)
                    0x1b, // ##_## 77
                    0x15, // #_#_# 0x4D
                    0x15, // #_#_#
                    0x11, // #___#
                    0x11, // #___#
                    0x11, // #___#
                    
                    0x11, // #___# N (verified from 2012-04-15 photo)
                    0x19, // ##__# 78
                    0x19, // ##__# 0x4E
                    0x15, // #_#_#
                    0x13, // #__##
                    0x13, // #__##
                    0x11, // #___#
                    
                    0x0e, // _###_ O (verified from 2012-04-15 photo, matches maint. man. fig. 3-6)
                    0x11, // #___# 79
                    0x11, // #___# 0x4F
                    0x11, // #___#
                    0x11, // #___#
                    0x11, // #___#
                    0x0e, // _###_

                    0x1e, // ####_ P (verified from 2012-04-15 photo)
                    0x11, // #___# 80
                    0x11, // #___# 0x50
                    0x1e, // ####_
                    0x10, // #____
                    0x10, // #____
                    0x10, // #____
                    
                    0x0e, // _###_  (verified from 2019-05-04 photo)
                    0x11, // #___# 81
                    0x11, // #___# 0x51
                    0x11, // #___#
                    0x15, // #_#_#
                    0x12, // #__#_
                    0x0d, // _##_#
                    
                    0x1e, // ####_ R (verified from 2012-04-15 photo)
                    0x11, // #___# 82
                    0x11, // #___# 0x52
                    0x1e, // ####_
                    0x14, // #_#__
                    0x12, // #__#_
                    0x11, // #___#

                    0x0f, // _#### S (verified from 2012-04-15 photo)
                    0x10, // #____ 83
                    0x10, // #____ 0x53
                    0x0e, // _###_
                    0x01, // ____#
                    0x01, // ____#
                    0x1e, // ####_

                    0x1f, // ##### T (verified from 2012-04-15 photo)
                    0x04, // __#__ 84
                    0x04, // __#__ 0x54
                    0x04, // __#__
                    0x04, // __#__
                    0x04, // __#__
                    0x04, // __#__

                    0x11, // #___# U (verified from 2019-05-04 photo)
                    0x11, // #___# 85
                    0x11, // #___# 0x55
                    0x11, // #___#
                    0x11, // #___#
                    0x11, // #___#
                    0x0e, // _###_
                    
                    0x11, // #___# V (verified from 2012-04-15 photo)
                    0x11, // #___# 86
                    0x11, // #___# 0x56
                    0x0a, // _#_#_
                    0x0a, // _#_#_
                    0x04, // __#__
                    0x04, // __#__

                    0x11, // #___# W (verified from 2012-04-15 photo)
                    0x11, // #___# 87
                    0x11, // #___# 0x57
                    0x11, // #___#
                    0x15, // #_#_#
                    0x1b, // ##_##
                    0x11, // #___#

                    0x11, // #___# X (verified from 2019-05-04 photo)
                    0x11, // #___# 88
                    0x0a, // _#_#_ 0x58
                    0x04, // __#__
                    0x0a, // _#_#_
                    0x11, // #___#
                    0x11, // #___#
                    
                    0x11, // #___# Y (verified from 2019-05-04 photo)
                    0x11, // #___# 89
                    0x0a, // _#_#_ 0x59
                    0x04, // __#__
                    0x04, // __#__
                    0x04, // __#__
                    0x04, // __#__
                    
                    0x1f, // ##### Z (verified from 2019-05-04 photo)
                    0x01, // ____# 90
                    0x02, // ___#_ 0x5A
                    0x04, // __#__
                    0x08, // _#___
                    0x10, // #____
                    0x1f, // #####
                    
                    0x0e, // _###_ [ (verified from 2019-05-04 photo)
                    0x08, // _#___ 91
                    0x08, // _#___ 0x5B
                    0x08, // _#___
                    0x08, // _#___
                    0x08, // _#___
                    0x0e, // _###_
                    
                    0x10, // #____ \ (verified from 2019-05-04 photo)
                    0x10, // #____ 92
                    0x08, // _#___ 0x5C
                    0x04, // __#__
                    0x02, // ___#_
                    0x01, // ____#
                    0x01, // ____#
                    
                    0x0e, // _###_ ] (verified from 2019-05-04 photo)
                    0x02, // ___#_ 93
                    0x02, // ___#_ 0x5D
                    0x02, // ___#_
                    0x02, // ___#_
                    0x02, // ___#_
                    0x0e, // _###_
                    
                    0x0e, // _###_ ^ (verified from 2019-05-04 photo)
                    0x11, // #___# 94
                    0x00, // _____ 0x5E
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    
                    0x00, // _____ _ (verified from 2019-05-04 photo)
                    0x00, // _____ 95
                    0x00, // _____ 0x5F
                    0x00, // _____
                    0x00, // _____
                    0x00, // _____
                    0x1f, // #####
                };
            }
        }


        // Terminal-I/O Interface and UART Timing

        public partial class VT05
        {
            private UART mUART;                     // UART emulator
            private String mCaption;                // desired window title bar caption
            private volatile Boolean mCaptionDirty; // true if caption has changed

            // called by main UI thread via constructor
            private void InitIO()
            {
                mUART = new UART(this);
                mUART.IO = new IO.Loopback(null);
                mCaption = String.Concat(Program.Name, " - ", mUART.IO.ConnectionString);
                mCaptionDirty = true;
            }

            public override String Caption
            {
                get { return mCaption; }
            }

            public override Boolean CaptionDirty
            {
                get { return mCaptionDirty; }
                set { mCaptionDirty = value; }
            }

            public override void Shutdown()
            {
                mUART.IO.Close();
            }

            private void SetBreakState(Boolean asserted)
            {
                mUART.IO.SetBreak(asserted);
            }

            private void SetTransmitSpeed(Int32 baudRate)
            {
                mUART.SetTransmitSpeed(baudRate);
            }

            private void SetReceiveSpeed(Int32 baudRate)
            {
                mUART.SetReceiveSpeed(baudRate);
            }

            private void SetTransmitParity(System.IO.Ports.Parity parity)
            {
                mUART.SetTransmitParity(parity);
            }

            private void Send(Byte data)
            {
                mUART.Send(data);
            }

            private class UART
            {
                private VT05 mVT05;             // for calling parent methods
                private Queue<Byte> mSendQueue; // bytes waiting to be fully sent by UART
                private Timer mSendTimer;       // UART byte transmit timer
                private Boolean mSendBusy;      // UART is transmitting bits
                private Int32 mSendSpeed;       // UART transmit baud rate
                private Double mSendRate;       // UART byte transmit rate
                private Int32 mSendPeriod;      // time (ms) for UART to send one byte
                private DateTime mSendClock;    // UART transmit clock
                private Int32 mSendCount;       // bytes transmitted since clock
                private Queue<Byte> mRecvQueue; // bytes waiting to be fully received by UART
                private Timer mRecvTimer;       // UART byte receive timer
                private Boolean mRecvBusy;      // UART is receiving bits
                private Int32 mRecvSpeed;       // UART receive baud rate
                private Double mRecvRate;       // UART byte receive rate
                private Int32 mRecvPeriod;      // time (ms) for UART to receive one byte
                private DateTime mRecvClock;    // UART receive clock
                private Int32 mRecvCount;       // bytes received since clock
                private Boolean mRecvBreak;     // receive break state
                private System.IO.Ports.Parity mParity;
                private IO mIO;                 // I/O interface

                public UART(VT05 parent)
                {
                    mVT05 = parent;
                    mSendQueue = new Queue<Byte>();
                    mSendTimer = new Timer(SendTimer_Callback, this, Timeout.Infinite, Timeout.Infinite);
                    SetTransmitSpeed(300);
                    mRecvQueue = new Queue<Byte>();
                    mRecvTimer = new Timer(RecvTimer_Callback, this, Timeout.Infinite, Timeout.Infinite);
                    SetReceiveSpeed(300);
                    SetTransmitParity(System.IO.Ports.Parity.Space);
                }

                public IO IO
                {
                    get
                    {
                        return mIO;
                    }
                    set
                    {
                        if (mIO == value) return;
                        if (mIO != null) mIO.Close();
                        mIO = value;
                        if (mIO != null) mIO.IOEvent += IOEvent;
                    }
                }

                public Int32 TransmitSpeed
                {
                    get { return mSendSpeed; }
                    set { SetTransmitSpeed(value); }
                }

                public Int32 ReceiveSpeed
                {
                    get { return mRecvSpeed; }
                    set { SetReceiveSpeed(value); }
                }

                public void SetTransmitSpeed(Int32 baudRate)
                {
                    lock (mSendQueue)
                    {
                        switch (baudRate)
                        {
                            case 0:
                                mSendSpeed = 0;
                                break;
                            case 19200:
                                mSendSpeed = 19200;
                                mSendRate = 1920;
                                mSendPeriod = 1;
                                break;
                            case 9600:
                                mSendSpeed = 9600;
                                mSendRate = 960;
                                mSendPeriod = 1;
                                break;
                            case 4800:
                                mSendSpeed = 4800;
                                mSendRate = 480;
                                mSendPeriod = 2;
                                break;
                            case 2400:
                                mSendSpeed = 2400;
                                mSendRate = 240;
                                mSendPeriod = 4;
                                break;
                            case 1200:
                                mSendSpeed = 1200;
                                mSendRate = 120;
                                mSendPeriod = 8;
                                break;
                            case 600:
                                mSendSpeed = 600;
                                mSendRate = 60;
                                mSendPeriod = 16;
                                break;
                            case 300:
                                mSendSpeed = 300;
                                mSendRate = 30;
                                mSendPeriod = 33;
                                break;
                            case 150:
                                mSendSpeed = 150;
                                mSendRate = 15;
                                mSendPeriod = 66;
                                break;
                            case 110:
                                mSendSpeed = 110;
                                mSendRate = 10;
                                mSendPeriod = 100;
                                break;
                            case 75:
                                mSendSpeed = 75;
                                mSendRate = 7.5;
                                mSendPeriod = 133;
                                break;
                            default:
                                throw new ArgumentException("baudRate");
                        }
                        if (mSendBusy)
                        {
                            mSendClock = DateTime.UtcNow;
                            mSendCount = 0;
                        }
                    }
                }

                public void SetReceiveSpeed(Int32 baudRate)
                {
                    lock (mRecvQueue)
                    {
                        switch (baudRate)
                        {
                            case 0:
                                mRecvSpeed = 0;
                                break;
                            case 19200:
                                mRecvSpeed = 19200;
                                mRecvRate = 1920;
                                mRecvPeriod = 1;
                                break;
                            case 9600:
                                mRecvSpeed = 9600;
                                mRecvRate = 960;
                                mRecvPeriod = 1;
                                break;
                            case 4800:
                                mRecvSpeed = 4800;
                                mRecvRate = 480;
                                mRecvPeriod = 2;
                                break;
                            case 2400:
                                mRecvSpeed = 2400;
                                mRecvRate = 240;
                                mRecvPeriod = 4;
                                break;
                            case 1200:
                                mRecvSpeed = 1200;
                                mRecvRate = 120;
                                mRecvPeriod = 8;
                                break;
                            case 600:
                                mRecvSpeed = 600;
                                mRecvRate = 60;
                                mRecvPeriod = 16;
                                break;
                            case 300:
                                mRecvSpeed = 300;
                                mRecvRate = 30;
                                mRecvPeriod = 33;
                                break;
                            case 150:
                                mRecvSpeed = 150;
                                mRecvRate = 15;
                                mRecvPeriod = 66;
                                break;
                            case 110:
                                mRecvSpeed = 110;
                                mRecvRate = 10;
                                mRecvPeriod = 100;
                                break;
                            case 75:
                                mRecvSpeed = 75;
                                mRecvRate = 7.5;
                                mRecvPeriod = 133;
                                break;
                            default:
                                throw new ArgumentException("baudRate");
                        }
                        if (mRecvBusy)
                        {
                            mRecvClock = DateTime.UtcNow;
                            mRecvCount = 0;
                        }
                    }
                }

                public void SetTransmitParity(System.IO.Ports.Parity parity)
                {
                    mParity = parity;
                }

                private Int32 NybbleParity(Int32 data)
                {
                    switch (data & 0x0F)
                    {
                        case 0x00: return 0;
                        case 0x01: return 1;
                        case 0x02: return 1;
                        case 0x03: return 0;
                        case 0x04: return 1;
                        case 0x05: return 0;
                        case 0x06: return 0;
                        case 0x07: return 1;
                        case 0x08: return 1;
                        case 0x09: return 0;
                        case 0x0A: return 0;
                        case 0x0B: return 1;
                        case 0x0C: return 0;
                        case 0x0D: return 1;
                        case 0x0E: return 1;
                        case 0x0F: return 0;
                        default: throw new ArgumentOutOfRangeException();
                    }
                }

                public void Send(Byte data)
                {
                    switch (mParity)
                    {
                        case System.IO.Ports.Parity.None:
                        case System.IO.Ports.Parity.Space:
                            break;

                        case System.IO.Ports.Parity.Mark:
                            data |= 0x80;
                            break;

                        case System.IO.Ports.Parity.Odd:
                            if ((NybbleParity(data >> 4) + NybbleParity(data)) != 1) data |= 0x80;
                            break;

                        case System.IO.Ports.Parity.Even:
                            if ((NybbleParity(data >> 4) + NybbleParity(data)) == 1) data |= 0x80;
                            break;
                    }

                    lock (mSendQueue)
                    {
                        if (mSendSpeed == 0)
                        {
                            mIO.Send(data);
                            return;
                        }
                        if ((!mSendBusy) && (!mIO.DelaySend)) mIO.Send(data);
                        else mSendQueue.Enqueue(data);
                        if (mVT05.mOptHalfDuplex && !(mIO is IO.Loopback)) IOEvent(this, new IOEventArgs(IOEventType.Data, data));
                        if (!mSendBusy)
                        {
                            mSendBusy = true;
                            mSendClock = DateTime.UtcNow;
                            mSendCount = 0;
                            mSendTimer.Change(0, mSendPeriod);
                        }
                    }
                }

                private void IOEvent(Object sender, IOEventArgs e)
                {
                    Debug.WriteLine("IOEvent: {0} {1} (0x{2:X2})", e.Type, (Char)e.Value, e.Value);
                    switch (e.Type)
                    {
                        case IOEventType.Data:
                            Byte data = (Byte)(e.Value & 0x7F); // ignore received parity
                            lock (mRecvQueue)
                            {
                                if (mRecvSpeed == 0)
                                {
                                    mVT05.Recv(data);
                                    return;
                                }
                                if ((!mRecvBusy) && (!mIO.DelayRecv)) mVT05.Recv(data);
                                else mRecvQueue.Enqueue(data);
                                if (!mRecvBusy)
                                {
                                    mRecvBusy = true;
                                    mRecvClock = DateTime.UtcNow;
                                    mRecvCount = 0;
                                    mRecvTimer.Change(0, mRecvPeriod);
                                }
                            }
                            break;
                        case IOEventType.Break:
                            lock (mRecvQueue) mRecvBreak = (e.Value != 0);
                            break;
                        case IOEventType.Flush:
                            lock (mRecvQueue) mRecvQueue.Clear();
                            break;
                        case IOEventType.Disconnect:
                            lock (mRecvQueue) mRecvQueue.Clear();
                            IO = new IO.Loopback(null);
                            mVT05.mCaption = String.Concat(Program.Name, " - ", IO.ConnectionString);
                            mVT05.mCaptionDirty = true;
                            break;
                    }
                }

                private void SendTimer_Callback(Object state)
                {
                    lock (mSendQueue)
                    {
                        TimeSpan t = DateTime.UtcNow.Subtract(mSendClock);
                        Int32 due = (Int32)(t.TotalSeconds * mSendRate + 0.5) - mSendCount;
                        Debug.WriteLine("SendTimer_Callback: due={0:D0} ct={1:D0}", due, mSendQueue.Count);
                        if (due <= 0) return;
                        while ((due-- > 0) && (mSendQueue.Count != 0))
                        {
                            mSendCount++;
                            mIO.Send(mSendQueue.Dequeue());
                        }
                        if (mSendQueue.Count == 0)
                        {
                            mSendTimer.Change(Timeout.Infinite, Timeout.Infinite);
                            mSendBusy = false;
                        }
                        else if (t.Minutes != 0)
                        {
                            mSendClock = DateTime.UtcNow;
                            mSendCount = 0;
                        }
                    }
                }

                private void RecvTimer_Callback(Object state)
                {
                    lock (mRecvQueue)
                    {
                        TimeSpan t = DateTime.UtcNow.Subtract(mRecvClock);
                        Int32 due = (Int32)(t.TotalSeconds * mRecvRate + 0.5) - mRecvCount;
                        Debug.WriteLine("RecvTimer_Callback: due={0:D0} ct={1:D0}", due, mRecvQueue.Count);
                        if (due <= 0) return;
                        while ((due-- > 0) && (mRecvQueue.Count != 0))
                        {
                            mRecvCount++;
                            mVT05.Recv(mRecvQueue.Dequeue());
                        }
                        if (mRecvQueue.Count == 0)
                        {
                            mRecvTimer.Change(Timeout.Infinite, Timeout.Infinite);
                            mRecvBusy = false;
                        }
                        else if (t.Minutes != 0)
                        {
                            mRecvClock = DateTime.UtcNow;
                            mRecvCount = 0;
                        }
                    }
                }
            }
        }
    }
}
