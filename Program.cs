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
        private const int MOD_CONTROL = 0x0002;

        // Constant for hotkey message
        private const int WM_HOTKEY = 0x0312;

        // Unique ID for hotkey
        private const int HOTKEY_ID = 1;

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private bool hotkeyRegistered = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [STAThread]
        static void Main()
        {
            Application.Run(new Program());
        }

        public Program()
        {
            // Create a simple tray menu
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Start", OnStart);
            trayMenu.MenuItems.Add("Stop", OnStop);
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Create a tray icon
            trayIcon = new NotifyIcon
            {
                Text = "Monitor Cursor Switcher",
                Icon = SystemIcons.Application, // Replace with a custom icon if needed
                ContextMenu = trayMenu,
                Visible = true
            };

            RegisterHotkey();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                MoveCursorToNextMonitor();
            }

            base.WndProc(ref m);
        }

        private void MoveCursorToNextMonitor()
        {
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

            var nextScreenCenter = new System.Drawing.Point(
                nextScreen.Bounds.Left + nextScreen.Bounds.Width / 2,
                nextScreen.Bounds.Top + nextScreen.Bounds.Height / 2
            );
            Cursor.Position = nextScreenCenter;

            Console.WriteLine($"Moved cursor to monitor {nextIndex + 1}.");
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
