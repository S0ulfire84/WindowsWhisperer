# Dauntless Firechant

Dauntless Firechant is a Windows desktop application that provides real-time speech-to-text functionality using OpenAI's Whisper model. It allows you to quickly transcribe your voice into text anywhere in your system by using a global keyboard shortcut.

## Features

- **Global Hotkey**: Press and hold `Windows + Ctrl` to start recording
- **Instant Transcription**: Release the hotkey combination to automatically transcribe your speech
- **System Tray Integration**: Runs quietly in the background with a system tray icon
- **Audio Feedback**: Plays a sound when recording starts
- **Automatic Text Insertion**: Transcribed text is automatically inserted at your cursor position

## Requirements

- Windows operating system
- .NET Framework
- OpenAI API key

## Setup

1. Download and install the application
2. On first run, you'll be prompted to enter your OpenAI API key
3. The API key will be saved in the `appsettings.json` file for future use

## How to Use

1. Launch the application
2. Press and hold `Windows + Ctrl` to start recording
3. Speak your message
4. Release the keys to stop recording and start transcription
5. The transcribed text will be automatically inserted at your cursor position

## Project Structure

The main components of the application are:

- `Program.cs`: Application entry point and initialization
- `BackgroundForm.cs`: Core functionality including keyboard hooks, audio recording, and OpenAI API integration
- `ConfigurationManager.cs`: Handles API key storage and configuration
- `appsettings.json`: Stores the OpenAI API key

## Technical Details

The application uses:
- NAudio for audio recording
- Windows API hooks for global keyboard shortcuts
- OpenAI's Whisper API for speech recognition
- System.Windows.Forms for the UI components

## Security Note

The OpenAI API key is stored in `appsettings.json`. Make sure to keep this file secure and never share it publicly.

## Troubleshooting

If the application isn't working:
1. Verify your OpenAI API key is correct
2. Ensure your microphone is properly connected and set as the default recording device
3. Check that you have an active internet connection
4. Make sure you have sufficient API credits in your OpenAI account 