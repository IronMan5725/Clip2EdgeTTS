# EdgeTTS

### This is an interface testing tool for the EdgeTTS library to intuitively test and verify the various functions of EdgeTTS.

![EdgeTTS Test Application Interface](EdgeTTSTest.png)

## Edge TTS 

由 AtmoOmen Modified ![Edge TTS](https://github.com/AtmoOmen/EdgeTTS)

## Tool introduction

This tool is mainly used to test and demonstrate the functions of the EdgeTTS library. 
Through the graphical interface, you can easily call various APIs of EdgeTTS without writing code。

## EdgeTTS API

EdgeTTS API:

### initialization

```csharp
// Create an EdgeTTS engine instance, specify cache folders and log callbacks
_edgeTts = new EdgeTTSEngine(cacheFolder, LogCallback);
```

### Voice synthesis and playback

```csharp
// Get all available voices
EdgeTTSEngine.Voices

// Read text asynchronously
await _edgeTts.SpeakAsync(text, settings);

// Stop reading
_edgeTts.Stop();

// Get audio files (cache)
string audioFile = await _edgeTts.GetAudioFileAsync(text, settings);
```

### Voice synthesis and playback

```csharp
//Get the system default audio device ID
int defaultDeviceId = EdgeTTSEngine.GetDefaultAudioDeviceId();

// Get a list of all audio devices
List<AudioDevice> devices = EdgeTTSEngine.GetAudioDevices();
```

### Setting options

```csharp
// Create TTS settings
var settings = new EdgeTTSSettings
{
    Voice = "zh-CN-XiaoxiaoNeural",  // voice
    Volume = 100,                    // volume (0-100)
    Speed = 100,                     // speed (0-200)
    Pitch = 100,                     // tone (0-200)
    AudioDeviceId = deviceId         // Audio output deviceID
};
```

### Resource release

```csharp
// Free up resources
_edgeTts.Dispose();
```

## Test tool features

- Voice selection and parameter adjustment
- Audio device selection
- Read text in real time
- Audio cache to file
- Detailed logging

## System requirements

- Windows 10/11
- .NET 8.0+
- Network connection
