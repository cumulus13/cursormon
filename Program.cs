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
        private IntPtr lastWindowMonitor1 = IntPtr.Zero;
        private IntPtr lastWindowMonitor2 = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

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
            public int Left, Top, Right, Bottom;
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

            this.HandleCreated += (sender, e) => { if (!hotkeyRegistered) RegisterHotkey(); };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                MoveCursorToNextMonitor();
                RestorePreviousWindow();
            }

            base.WndProc(ref m);
        }

        private void MoveCursorToNextMonitor()
        {
            Console.WriteLine("Run move cursor .....");
            var cursorPosition = Cursor.Position;
            var screens = Screen.AllScreens;
            var currentScreen = Array.Find(screens, s => s.Bounds.Contains(cursorPosition));

            if (currentScreen == null) return;

            var currentIndex = Array.IndexOf(screens, currentScreen);
            var nextIndex = (currentIndex + 1) % screens.Length;
            var nextScreen = screens[nextIndex];

            if (currentIndex == 0)
            {
                lastWindowMonitor1 = GetForegroundWindow();
            }
            else
            {
                lastWindowMonitor2 = GetForegroundWindow();
            }

            Cursor.Position = new Point(nextScreen.Bounds.Left + nextScreen.Bounds.Width / 2, nextScreen.Bounds.Top + nextScreen.Bounds.Height / 2);
        }

        private void RestorePreviousWindow()
        {
            var cursorPosition = Cursor.Position;
            var screens = Screen.AllScreens;
            var currentScreen = Array.Find(screens, s => s.Bounds.Contains(cursorPosition));

            if (currentScreen == null) return;

            var currentIndex = Array.IndexOf(screens, currentScreen);
            if (currentIndex == 0 && lastWindowMonitor1 != IntPtr.Zero)
            {
                SetForegroundWindow(lastWindowMonitor1);
                lastWindowMonitor1 = IntPtr.Zero;
            }
            else if (currentIndex == 1 && lastWindowMonitor2 != IntPtr.Zero)
            {
                SetForegroundWindow(lastWindowMonitor2);
                lastWindowMonitor2 = IntPtr.Zero;
            }
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

