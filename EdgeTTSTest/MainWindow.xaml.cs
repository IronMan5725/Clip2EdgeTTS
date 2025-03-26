using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EdgeTTS;
using Path = System.IO.Path;

namespace EdgeTTSTest;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private EdgeTTSEngine? _edgeTts;
    private readonly string _cacheFolder;
    private bool _isSpeaking;
    private DateTime _lastConnectionTime = DateTime.Now;
    private readonly TimeSpan _connectionTimeout = TimeSpan.FromMinutes(5); // 5分钟后重置连接

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

        LoadVoices();

        TextToSpeech.Text = "测试TTS";

        var connectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        connectionTimer.Tick += CheckConnection;
        connectionTimer.Start();

        LogMessage("EdgeTTS测试应用已初始化完成");
    }

    private void CheckConnection(object? sender, EventArgs e)
    {
        if (_edgeTts == null) return;

        // 如果超过设定的超时时间，重置连接
        if (DateTime.Now - _lastConnectionTime > _connectionTimeout)
        {
            LogMessage("WebSocket连接可能已超时，正在重置连接...");
            try
            {
                _edgeTts.Dispose();
                _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
                _lastConnectionTime = DateTime.Now;
                LogMessage("EdgeTTS引擎已重新初始化");
            }
            catch (Exception ex)
            {
                LogMessage($"重置连接时出错: {ex.Message}");
            }
        }
    }

    private void UpdateConnectionTime()
    {
        _lastConnectionTime = DateTime.Now;
    }

    private void InitializeTts()
    {
        try
        {
            _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
            _lastConnectionTime = DateTime.Now;
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
            var displayText = $"{voice}";
            VoiceComboBox.Items.Add(displayText);
        }

        for (var i = 0; i < EdgeTTSEngine.Voices.Length; i++)
        {
            var voiceId = EdgeTTSEngine.Voices[i].ToString();
            if (!voiceId.Contains("zh-CN-XiaoxiaoNeural")) continue;
            VoiceComboBox.SelectedIndex = i;
            break;
        }

        if (VoiceComboBox.SelectedIndex < 0 && VoiceComboBox.Items.Count > 0)
        {
            VoiceComboBox.SelectedIndex = 0;
        }

        LogMessage($"已加载 {EdgeTTSEngine.Voices.Length} 个语音");
    }

    private EdgeTTSSettings CreateSettings()
    {
        var selectedVoiceIndex = VoiceComboBox.SelectedIndex;
        if (selectedVoiceIndex < 0) selectedVoiceIndex = 0;

        return new EdgeTTSSettings
        {
            Voice = EdgeTTSEngine.Voices[selectedVoiceIndex].ToString(),
            Volume = (int)VolumeSlider.Value,
            Speed = (int)SpeedSlider.Value,
            Pitch = (int)PitchSlider.Value
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

                        UpdateConnectionTime();
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
                        UpdateConnectionTime();
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
            MessageBox.Show($"SpeakButton_Click方法出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // 添加重试逻辑
                const int maxRetries = 3;
                int retryCount = 0;
                string audioFile = string.Empty;

                while (string.IsNullOrEmpty(audioFile) && retryCount < maxRetries)
                {
                    try
                    {
                        if (retryCount > 0)
                        {
                            LogMessage($"尝试重新连接... (第{retryCount}次)");
                            await Task.Delay(1000); // 重试前等待1秒
                        }

                        UpdateConnectionTime(); // 更新连接时间
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
                        UpdateConnectionTime(); // 更新连接时间
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
            MessageBox.Show($"CacheButton_Click方法出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_edgeTts == null)
        {
            MessageBox.Show("EdgeTTS引擎未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            LogMessage("正在手动重置WebSocket连接...");
            _edgeTts.Dispose();
            _edgeTts = new EdgeTTSEngine(_cacheFolder, LogMessage);
            _lastConnectionTime = DateTime.Now;
            LogMessage("EdgeTTS引擎已重新初始化，WebSocket连接已重置");
            MessageBox.Show("WebSocket连接已重置", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogMessage($"重置连接时出错: {ex.Message}");
            MessageBox.Show($"重置连接时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}