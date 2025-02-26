using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsWhispererWidget
{
    public partial class BackgroundForm : Form
    {
        // Constants for modifiers
        const int MOD_CONTROL = 0x2;
        const int MOD_SHIFT = 0x4;
        const int WM_HOTKEY = 0x0312;

        // Hotkey ID
        const int HOTKEY_ID = 9000;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public BackgroundForm()
        {
            InitializeComponent();
            // Hide the form and remove from taskbar
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Register the hotkey after the handle is created
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.T);
        }

        // Override WndProc to listen for hotkey messages
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                Console.WriteLine("Hotkey Ctrl+Shift+T was pressed.");
                // When hotkey is pressed, insert text at cursor position
                InsertTextAtCursor("Hello World!");
            }
            base.WndProc(ref m);
        }

        void InsertTextAtCursor(string text)
        {
            try
            {
                // Store current clipboard content
                string previousClipboardText = Clipboard.GetText();

                // Set new text to clipboard
                Clipboard.SetText(text);

                // Simulate Ctrl+V to paste
                SendKeys.SendWait("^v");

                // Restore previous clipboard content
                if (!string.IsNullOrEmpty(previousClipboardText))
                {
                    Clipboard.SetText(previousClipboardText);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting text: {ex.Message}");
            }
        }

        // Unregister the hotkey when closing the form/application
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }
    }
} 