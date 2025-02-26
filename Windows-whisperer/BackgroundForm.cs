using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio.Wave;
using Microsoft.Extensions.Configuration;
using OpenAI_API;
using System.IO;

namespace WindowsWhispererWidget
{
    public partial class BackgroundForm : Form
    {
        // Constants for modifiers
        const int MOD_CONTROL = 0x2;
        const int MOD_SHIFT = 0x4;
        const int WM_HOTKEY = 0x0312;
        const int WM_KEYUP = 0x0101;

        // Hotkey ID
        const int HOTKEY_ID = 9000;

        private WaveInEvent waveSource;
        private MemoryStream audioStream;
        private bool isRecording = false;
        private string openAiApiKey;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public BackgroundForm()
        {
            InitializeComponent();
            LoadConfiguration();
            InitializeAudioRecording();
            
            // Hide the form and remove from taskbar
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private void LoadConfiguration()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            openAiApiKey = config["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                MessageBox.Show("Please set your OpenAI API key in appsettings.json");
                Application.Exit();
            }
        }

        private void InitializeAudioRecording()
        {
            waveSource = new WaveInEvent();
            waveSource.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono, which is good for speech recognition
            waveSource.DataAvailable += WaveSource_DataAvailable;
        }

        private void WaveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (audioStream != null && isRecording)
            {
                audioStream.Write(e.Buffer, 0, e.BytesRecorded);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.T);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                if (!isRecording)
                {
                    StartRecording();
                }
            }
            else if (m.Msg == WM_KEYUP)
            {
                // Check if either Ctrl or Shift is released
                Keys keyCode = (Keys)m.WParam.ToInt32();
                if ((keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey) && isRecording)
                {
                    StopRecordingAndTranscribe();
                }
            }
            base.WndProc(ref m);
        }

        private void StartRecording()
        {
            try
            {
                audioStream = new MemoryStream();
                isRecording = true;
                waveSource.StartRecording();
                Console.WriteLine("Recording started...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting recording: {ex.Message}");
            }
        }

        private async void StopRecordingAndTranscribe()
        {
            try
            {
                isRecording = false;
                waveSource.StopRecording();
                Console.WriteLine("Recording stopped...");

                // Convert to WAV
                byte[] audioData = audioStream.ToArray();
                audioStream.Dispose();
                audioStream = null;

                // Save to temporary file
                string tempFile = Path.Combine(Path.GetTempPath(), "recording.wav");
                File.WriteAllBytes(tempFile, audioData);

                // Initialize OpenAI API client
                var api = new OpenAI_API.OpenAIAPI(openAiApiKey);

                // Transcribe using Whisper
                var result = await api.Audio.CreateTranscriptionAsync(tempFile);
                
                // Insert transcribed text at cursor
                InsertTextAtCursor(result.Text);

                // Clean up temp file
                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing audio: {ex.Message}");
            }
        }

        void InsertTextAtCursor(string text)
        {
            try
            {
                string previousClipboardText = Clipboard.GetText();
                Clipboard.SetText(text);
                SendKeys.SendWait("^v");
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (waveSource != null)
            {
                waveSource.Dispose();
            }
            if (audioStream != null)
            {
                audioStream.Dispose();
            }
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }
    }
} 