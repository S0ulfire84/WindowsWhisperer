using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsWhispererWidget
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AllocConsole();
            Console.WriteLine("Application started. Press and hold 'Windows + Ctrl' to start recording, release to transcribe...");
            Application.Run(new BackgroundForm());
        }
    }
} 