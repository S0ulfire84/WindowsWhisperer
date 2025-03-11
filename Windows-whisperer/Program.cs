using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Configuration;

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

            // Check for API key
            string apiKey = ConfigurationManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("OpenAI API key not found. Please enter your API key:");
                apiKey = Console.ReadLine();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    ConfigurationManager.SaveApiKey(apiKey);
                }
                else
                {
                    Console.WriteLine("No API key provided. Application will exit.");
                    return;
                }
            }

            Console.WriteLine("Application started. Press and hold 'Windows + Ctrl' to start recording, release to transcribe...");
            Application.Run(new BackgroundForm());
        }
    }
} 