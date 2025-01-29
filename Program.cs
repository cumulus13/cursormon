using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CursorMon
{
    class Program : Form
    {
        // Constants for hotkey modifiers
        private const int MOD_ALT = 0x0001;
        private const int MOD_SHIFT = 0x0004;

        // Constant for hotkey message
        private const int WM_HOTKEY = 0x0312;

        // Unique ID for hotkey
        private const int HOTKEY_ID = 1;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool hotkeyRegistered = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();


        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [STAThread]
        static void Main()
        {
            Application.Run(new Program());
        }

        public Program()
        {
            // Create a simple tray menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Start", Image.FromFile(@"start.png"), OnStart);
            trayMenu.Items.Add("Stop", Image.FromFile(@"stop.png"), OnStop);
            trayMenu.Items.Add("Exit", Image.FromFile(@"exit.png"), OnExit);

            // Create a tray icon
            trayIcon = new NotifyIcon
            {
                Text = "Monitor Cursor Switcher",
                Icon = new Icon("CursorMon.icon.ico"),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            // Ensure form is hidden
            this.Load += (sender, e) => this.Hide();
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;

            // Ensure hotkey is registered after handle is created
            this.HandleCreated += (sender, e) =>
            {
                if (!hotkeyRegistered)
                {
                    RegisterHotkey();
                }
            };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                MoveCursorToNextMonitor();
                BringLastWindowToForeground();
            }

            base.WndProc(ref m);
        }

        private void MoveCursorToNextMonitor()
        {
            Console.WriteLine("Run move cursor .....");
            var cursorPosition = Cursor.Position;
            var screens = Screen.AllScreens;

            Screen currentScreen = null;
            foreach (var screen in screens)
            {
                if (screen.Bounds.Contains(cursorPosition))
                {
                    currentScreen = screen;
                    break;
                }
            }

            if (currentScreen == null) return;

            var currentIndex = Array.IndexOf(screens, currentScreen);
            var nextIndex = (currentIndex + 1) % screens.Length;
            var nextScreen = screens[nextIndex];

            var nextScreenCenter = new Point(
                nextScreen.Bounds.Left + nextScreen.Bounds.Width / 2,
                nextScreen.Bounds.Top + nextScreen.Bounds.Height / 2
            );
            Cursor.Position = nextScreenCenter;

            Console.WriteLine($"Moved cursor to monitor {nextIndex + 1}.");
        }

        private void BringLastWindowToForeground()
        {
            Console.WriteLine("Run bring to foreground .....");
            var cursorPosition = Cursor.Position;
            IntPtr lastForegroundWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == GetForegroundWindow()) return true; // Skip the currently focused window

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    var windowBounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

                    if (windowBounds.Contains(cursorPosition))
                    {
                        lastForegroundWindow = hWnd;
                        return false; // Stop enumeration
                    }
                }

                return true;
            }, IntPtr.Zero);

            if (lastForegroundWindow != IntPtr.Zero)
            {
                SetForegroundWindowWithPrivileges(lastForegroundWindow);
                Console.WriteLine("Set last window to foreground.");
            }
            else
            {
                Console.WriteLine("No window found on the current monitor.");
            }
        }

        private void SetForegroundWindowWithPrivileges(IntPtr hWnd)
        {
            uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);
            uint currentThreadId = GetCurrentThreadId();

            if (targetThreadId != currentThreadId)
            {
                // Attach the input of the two threads to allow foreground switching
                AttachThreadInput(currentThreadId, targetThreadId, true);
                SetForegroundWindow(hWnd);
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
            }

            // Allow our process to set the foreground window
            AllowSetForegroundWindow(-1);
        }

        private void RegisterHotkey()
        {
            if (!hotkeyRegistered)
            {
                if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT | MOD_SHIFT, (int)Keys.None))
                {
                    MessageBox.Show("Failed to register hotkey (Alt+Shift). Try running as administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                hotkeyRegistered = true;
                Console.WriteLine("Hotkey Alt+Shift registered.");
            }
        }

        private void UnregisterHotkey()
        {
            if (hotkeyRegistered)
            {
                UnregisterHotKey(this.Handle, HOTKEY_ID);
                hotkeyRegistered = false;
                Console.WriteLine("Hotkey unregistered.");
            }
        }

        private void OnStart(object sender, EventArgs e)
        {
            RegisterHotkey();
        }

        private void OnStop(object sender, EventArgs e)
        {
            UnregisterHotkey();
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnClosed(EventArgs e)
        {
            UnregisterHotkey();
            trayIcon.Dispose();
            base.OnClosed(e);
        }
    }
}
