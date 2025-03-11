using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio.Wave;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;
using System.Media;

namespace WindowsWhispererWidget
{
    public partial class BackgroundForm : Form
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private bool isCtrlDown = false;
        private bool isWinDown = false;
        private WaveInEvent waveSource;
        private MemoryStream audioStream;
        private bool isRecording = false;
        private string openAiApiKey;
        private readonly HttpClient httpClient;
        private IntPtr _hookID = IntPtr.Zero;
        private bool isProcessing = false;
        private NotifyIcon? _notifyIcon;
        private readonly SoundPlayer recordingSound;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public BackgroundForm()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            LoadConfiguration();
            InitializeAudioRecording();
            
            // Initialize sound player with the custom sound
            string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "start-recording.wav");
            recordingSound = new SoundPlayer(soundPath);
            
            _proc = HookCallback;
            _hookID = SetHook(_proc);

            // Initialize NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true
            };

            // Hide the form and remove from taskbar
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    if (key == Keys.LControlKey || key == Keys.RControlKey)
                    {
                        isCtrlDown = true;
                        CheckKeyCombo();
                    }
                    else if (key == Keys.LWin || key == Keys.RWin)
                    {
                        isWinDown = true;
                        CheckKeyCombo();
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    if (key == Keys.LControlKey || key == Keys.RControlKey)
                    {
                        isCtrlDown = false;
                        if (isRecording)
                        {
                            StopRecordingAndTranscribe();
                        }
                    }
                    else if (key == Keys.LWin || key == Keys.RWin)
                    {
                        isWinDown = false;
                        if (isRecording)
                        {
                            StopRecordingAndTranscribe();
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
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
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);
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

        private void CheckKeyCombo()
        {
            if (isCtrlDown && isWinDown && !isRecording && !isProcessing)
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            try
            {
                // Dispose of any existing audio stream
                if (audioStream != null)
                {
                    audioStream.Dispose();
                    audioStream = null;
                }
                
                audioStream = new MemoryStream();
                isRecording = true;
                waveSource.StartRecording();
                recordingSound.Play();
                Console.WriteLine("Recording started...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting recording: {ex.Message}");
            }
        }

        private async void StopRecordingAndTranscribe()
        {
            if (isProcessing) return;
            
            isProcessing = true;
            recordingSound.Play();
            Console.WriteLine("Processing audio...");

            string tempFile = Path.Combine(Path.GetTempPath(), "recording.wav");
            try
            {
                isRecording = false;
                waveSource.StopRecording();
                Console.WriteLine("Recording stopped...");

                if (audioStream == null || audioStream.Length == 0)
                {
                    Console.WriteLine("No audio data recorded");
                    _notifyIcon?.ShowBalloonTip(2000, "Error", "No audio data recorded", ToolTipIcon.Error);
                    isProcessing = false;
                    return;
                }

                // Convert memory stream to WAV file with proper headers
                audioStream.Position = 0;
                using (var writer = new WaveFileWriter(tempFile, waveSource.WaveFormat))
                {
                    audioStream.Position = 0;
                    audioStream.CopyTo(writer);
                }
                
                audioStream.Dispose();
                audioStream = null;

                Console.WriteLine($"Created WAV file at: {tempFile}");
                Console.WriteLine($"File size: {new FileInfo(tempFile).Length} bytes");

                // Create multipart form content and ensure all streams are properly disposed
                using (var formData = new MultipartFormDataContent())
                {
                    using (var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var fileContent = new StreamContent(fileStream))
                    {
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                        formData.Add(fileContent, "file", "recording.wav");
                        formData.Add(new StringContent("whisper-1"), "model");

                        Console.WriteLine("Sending request to Whisper API...");
                        var response = await httpClient.PostAsync("audio/transcriptions", formData);
                        var responseContent = await response.Content.ReadAsStringAsync();
                        // Console.WriteLine($"Raw API Response: {responseContent}");

                        if (response.IsSuccessStatusCode)
                        {
                            try 
                            {
                                var options = new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                };
                                var result = JsonSerializer.Deserialize<WhisperResponse>(responseContent, options);
                                Console.WriteLine($"Deserialized result: {result?.Text ?? "null"}");
                                
                                if (!string.IsNullOrEmpty(result?.Text))
                                {
                                    Console.WriteLine($"Received transcription: {result.Text}");
                                    InsertTextAtCursor(result.Text);
                                }
                                else
                                {
                                    Console.WriteLine("No transcription text in the response");
                                    _notifyIcon?.ShowBalloonTip(2000, "Error", "No transcription received from the API", ToolTipIcon.Error);
                                }
                            }
                            catch (JsonException ex)
                            {
                                Console.WriteLine($"JSON Deserialization error: {ex.Message}");
                                Console.WriteLine($"Response content was: {responseContent}");
                                _notifyIcon?.ShowBalloonTip(2000, "Error", "Failed to process API response", ToolTipIcon.Error);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"API Error Response: {responseContent}");
                            _notifyIcon?.ShowBalloonTip(2000, "Error", "Transcription failed", ToolTipIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception details: {ex}");
                _notifyIcon?.ShowBalloonTip(2000, "Error", "Error processing audio", ToolTipIcon.Error);
            }
            finally
            {
                isProcessing = false;
                
                // Ensure we always try to delete the temporary file
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                        Console.WriteLine("Temporary file deleted successfully");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting temporary file: {ex.Message}");
                }
            }
        }

        void InsertTextAtCursor(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Console.WriteLine("No text to insert");
                return;
            }

            string previousClipboardText = "";
            bool insertionFailed = false;
            try
            {
                // Store previous clipboard content if any
                if (Clipboard.ContainsText())
                {
                    previousClipboardText = Clipboard.GetText();
                }

                // Clear clipboard before setting new text
                Clipboard.Clear();
                Clipboard.SetText(text);
                SendKeys.SendWait("^v");
            }
            catch (Exception ex)
            {
                insertionFailed = true;
                Console.WriteLine($"Error inserting text: {ex.Message}");
                // Keep the transcribed text in clipboard instead of restoring previous content
                try 
                {
                    Clipboard.SetText(text);
                    _notifyIcon?.ShowBalloonTip(3000, "Transcription Available", 
                        "Failed to insert text at cursor position. Your transcription is available in the clipboard.", 
                        ToolTipIcon.Info);
                }
                catch (Exception clipEx)
                {
                    Console.WriteLine($"Error setting clipboard: {clipEx.Message}");
                    _notifyIcon?.ShowBalloonTip(2000, "Error", 
                        "Failed to insert text and store in clipboard", 
                        ToolTipIcon.Error);
                }
            }
            finally
            {
                // Only restore previous clipboard content if insertion succeeded
                if (!insertionFailed)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(previousClipboardText))
                        {
                            Clipboard.SetText(previousClipboardText);
                        }
                        else
                        {
                            Clipboard.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error restoring clipboard: {ex.Message}");
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
            }
            if (waveSource != null)
            {
                waveSource.Dispose();
            }
            if (audioStream != null)
            {
                audioStream.Dispose();
            }
            if (httpClient != null)
            {
                httpClient.Dispose();
            }
            if (_notifyIcon != null)
            {
                _notifyIcon.Dispose();
            }
            if (recordingSound != null)
            {
                recordingSound.Dispose();
            }
            base.OnFormClosing(e);
        }

        private class WhisperResponse
        {
            public string? Text { get; set; }
        }
    }
} 