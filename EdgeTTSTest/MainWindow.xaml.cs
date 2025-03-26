using System.Diagnostics;
using System.IO;
using System.Windows;
using EdgeTTS;
using Path = System.IO.Path;

namespace EdgeTTSTest;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string DefaultVoice = "zh-CN-XiaoxiaoNeural";
    private EdgeTTSEngine? _edgeTts;
    private readonly string _cacheFolder;
    private bool _isSpeaking;
    private int _currentAudioDeviceId;
    private readonly List<AudioDevice> _audioDeviceList;

    public MainWindow()
    {
        InitializeComponent();

        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EdgeTTSTest", "Cache");

        if (!Directory.Exists(_cacheFolder))
        {
            Directory.CreateDirectory(_cacheFolder);
        }

        InitializeTts();

        _currentAudioDeviceId = EdgeTTSEngine.GetDefaultAudioDeviceId();
        _audioDeviceList = EdgeTTSEngine.GetAudioDevices();
        LoadAudioDevices();
        LogMessage($"默认音频设备: {_currentAudioDeviceId}");

        LoadVoices();
        TextToSpeech.Text = "测试TTS";

        VolumeValueText.Text = ((int)VolumeSlider.Value).ToString();
        SpeedValueText.Text = ((int)SpeedSlider.Value).ToString();
        PitchValueText.Text = ((int)PitchSlider.Value).ToString();

        LogMessage("EdgeTTS测试应用已初始化完成");
    }

    private void InitializeTts()
    {
        try
        {
            _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
            LogMessage("EdgeTTS引擎已初始化");
        }
        catch (Exception ex)
        {
            LogMessage($"初始化EdgeTTS失败: {ex.Message}");
            MessageBox.Show($"初始化EdgeTTS失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadVoices()
    {
        VoiceComboBox.Items.Clear();

        foreach (var voice in EdgeTTSEngine.Voices)
        {
            var displayText = $"{voice.DisplayName} ({voice.Value})";
            VoiceComboBox.Items.Add(displayText);
        }

        for (var i = 0; i < EdgeTTSEngine.Voices.Length; i++)
        {
            var voiceId = EdgeTTSEngine.Voices[i].ToString();
            if (!voiceId.Contains(DefaultVoice)) continue;
            VoiceComboBox.SelectedIndex = i;
            break;
        }

        if (VoiceComboBox.SelectedIndex < 0 && VoiceComboBox.Items.Count > 0)
        {
            VoiceComboBox.SelectedIndex = 0;
        }

        LogMessage($"已加载 {EdgeTTSEngine.Voices.Length} 个语音");
    }

    private void LoadAudioDevices()
    {
        AudioDeviceComboBox.Items.Clear();

        foreach (var device in _audioDeviceList)
        {
            AudioDeviceComboBox.Items.Add($"{device.Name} (ID: {device.Id})");
        }

        // 设置默认设备为选中项
        for (var i = 0; i < _audioDeviceList.Count; i++)
        {
            if (_audioDeviceList[i].Id != _currentAudioDeviceId) continue;
            AudioDeviceComboBox.SelectedIndex = i;
            break;
        }

        if (AudioDeviceComboBox.SelectedIndex < 0 && AudioDeviceComboBox.Items.Count > 0)
        {
            AudioDeviceComboBox.SelectedIndex = 0;
            _currentAudioDeviceId = _audioDeviceList[0].Id;
        }

        LogMessage($"已加载 {_audioDeviceList.Count} 个音频设备");
    }

    private void AudioDeviceComboBox_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AudioDeviceComboBox.SelectedIndex < 0 ||
            AudioDeviceComboBox.SelectedIndex >= _audioDeviceList.Count) return;
        _currentAudioDeviceId = _audioDeviceList[AudioDeviceComboBox.SelectedIndex].Id;
        LogMessage(
            $"已选择音频设备: {_audioDeviceList[AudioDeviceComboBox.SelectedIndex].Name} (ID: {_currentAudioDeviceId})");
    }

    private EdgeTTSSettings CreateSettings()
    {
        var selectedVoiceIndex = VoiceComboBox.SelectedIndex;
        if (selectedVoiceIndex < 0) selectedVoiceIndex = 0;

        return new EdgeTTSSettings
        {
            Voice = EdgeTTSEngine.Voices[selectedVoiceIndex].Value,
            Volume = (int)VolumeSlider.Value,
            Speed = (int)SpeedSlider.Value,
            Pitch = (int)PitchSlider.Value,
            AudioDeviceId = _currentAudioDeviceId
        };
    }

    private void LogMessage(string message)
    {
        try
        {
            if (Dispatcher.CheckAccess())
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            }
            else
            {
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                        LogTextBox.ScrollToEnd();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"记录消息到UI时发生错误: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LogMessage方法出错: {ex.Message}");
        }
    }

    private async void SpeakButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_edgeTts == null)
            {
                MessageBox.Show("EdgeTTS引擎未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_isSpeaking)
            {
                LogMessage("已有语音正在播放，请等待完成或点击停止");
                return;
            }

            var text = TextToSpeech.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("请输入要朗读的文本", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _isSpeaking = true;
                SpeakButton.IsEnabled = false;

                var settings = CreateSettings();
                LogMessage(
                    $"开始朗读文本 (语音: {settings.Voice}, 音量: {settings.Volume}, 语速: {settings.Speed}, 音调: {settings.Pitch})");

                const int maxRetries = 3;
                var retryCount = 0;
                var success = false;

                while (!success && retryCount < maxRetries)
                {
                    try
                    {
                        if (retryCount > 0)
                        {
                            LogMessage($"尝试重新连接... (第{retryCount}次)");
                            await Task.Delay(1000);
                        }

                        await _edgeTts.SpeakAsync(text, settings);
                        success = true;
                        LogMessage("朗读完成");
                    }
                    catch (Exception ex) when (ex.Message.Contains("WebSocket") ||
                                               ex.InnerException?.Message.Contains("WebSocket") == true)
                    {
                        retryCount++;
                        LogMessage($"WebSocket连接断开: {ex.Message}");

                        if (retryCount >= maxRetries)
                        {
                            throw;
                        }

                        // 重新初始化EdgeTTS引擎
                        _edgeTts.Dispose();
                        _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
                        LogMessage("EdgeTTS引擎已重新初始化");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"朗读失败: {ex.Message}");
                MessageBox.Show($"朗读失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSpeaking = false;
                SpeakButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"SpeakButton_Click方法出错: {ex.Message}");
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_edgeTts == null) return;

        try
        {
            _edgeTts.Stop();
            LogMessage("已停止朗读");
        }
        catch (Exception ex)
        {
            LogMessage($"停止朗读失败: {ex.Message}");
        }
    }

    private async void CacheButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_edgeTts == null)
            {
                MessageBox.Show("EdgeTTS引擎未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var text = TextToSpeech.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("请输入要缓存的文本", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                CacheButton.IsEnabled = false;

                var settings = CreateSettings();
                LogMessage($"开始缓存文本音频 (语音: {settings.Voice}, 语速: {settings.Speed}, 音调: {settings.Pitch})");

                const int maxRetries = 3;
                var retryCount = 0;
                var audioFile = string.Empty;

                while (string.IsNullOrEmpty(audioFile) && retryCount < maxRetries)
                {
                    try
                    {
                        if (retryCount > 0)
                        {
                            LogMessage($"尝试重新连接... (第{retryCount}次)");
                            await Task.Delay(1000); // 重试前等待1秒
                        }

                        audioFile = await _edgeTts.GetAudioFileAsync(text, settings);
                        LogMessage($"音频缓存完成: {audioFile}");
                    }
                    catch (Exception ex) when (ex.Message.Contains("WebSocket") ||
                                               ex.InnerException?.Message.Contains("WebSocket") == true)
                    {
                        retryCount++;
                        LogMessage($"WebSocket连接断开: {ex.Message}");

                        if (retryCount >= maxRetries)
                        {
                            throw; // 重试次数用完，抛出异常
                        }

                        // 重新初始化EdgeTTS引擎
                        _edgeTts.Dispose();
                        _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
                        LogMessage("EdgeTTS引擎已重新初始化");
                    }
                }

                if (!string.IsNullOrEmpty(audioFile))
                {
                    MessageBox.Show($"音频缓存成功\n文件路径: {audioFile}", "成功", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"缓存音频失败: {ex.Message}");
                MessageBox.Show($"缓存音频失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CacheButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"CacheButton_Click方法出错: {ex.Message}");
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 重置所有设置到默认值
            VolumeSlider.Value = 100;
            SpeedSlider.Value = 100;
            PitchSlider.Value = 100;

            // 重置音频设备为默认设备
            _currentAudioDeviceId = EdgeTTSEngine.GetDefaultAudioDeviceId();

            // 选择默认音频设备
            for (int i = 0; i < _audioDeviceList.Count; i++)
            {
                if (_audioDeviceList[i].Id == _currentAudioDeviceId)
                {
                    AudioDeviceComboBox.SelectedIndex = i;
                    break;
                }
            }

            // 尝试重置为默认中文语音
            var foundChineseVoice = false;
            for (var i = 0; i < EdgeTTSEngine.Voices.Length; i++)
            {
                var voiceId = EdgeTTSEngine.Voices[i].ToString();
                if (!voiceId.Contains(DefaultVoice)) continue;
                VoiceComboBox.SelectedIndex = i;
                foundChineseVoice = true;
                break;
            }

            if (!foundChineseVoice && VoiceComboBox.Items.Count > 0)
            {
                VoiceComboBox.SelectedIndex = 0;
            }

            // 重置TTS引擎
            if (_edgeTts != null)
            {
                _edgeTts.Dispose();
                _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
                LogMessage("EdgeTTS引擎已重新初始化");
            }
            else
            {
                InitializeTts();
            }

            LogMessage($"所有设置已恢复默认值，默认音频设备ID: {_currentAudioDeviceId}");
            MessageBox.Show("所有设置已恢复默认值", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogMessage($"重置失败: {ex.Message}");
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;

        if (Equals(sender, VolumeSlider) && VolumeValueText != null)
        {
            VolumeValueText.Text = ((int)VolumeSlider.Value).ToString();
        }
        else if (Equals(sender, SpeedSlider) && SpeedValueText != null)
        {
            SpeedValueText.Text = ((int)SpeedSlider.Value).ToString();
        }
        else if (Equals(sender, PitchSlider) && PitchValueText != null)
        {
            PitchValueText.Text = ((int)PitchSlider.Value).ToString();
        }
    }
}