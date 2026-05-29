using InfoPanel.Plugins;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.Obs
{
    public class ObsPlugin : BasePlugin
    {
        private readonly OBSWebsocket _obs = new();
        private string _websocketPassword = "InfoPanelOBSPlugin"; // Default password

        // v1.1.0: Saved in user's Documents folder to guarantee write permissions and prevent path loss
        public override string? ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "InfoPanel", "OBS", "config.ini"
        );

        // --- GROUP 1: TEXT SENSORS (ON/OFF) ---
        private readonly PluginText _isRecordingText = new("obs_recording_text", "OBS Recording (Text)", "OFF");
        private readonly PluginText _isStreamingText = new("obs_streaming_text", "OBS Streaming (Text)", "OFF");
        private readonly PluginText _isReplayBufferText = new("obs_replay_text", "OBS Replay Buffer (Text)", "OFF");

        // Braille space "\u2800" used as default empty value to prevent the panel from falling back to "0"
        private readonly PluginText _replayAlertText = new("obs_replay_alert_text", "Save Status (Text)", "\u2800");

        // --- GROUP 2: NUMERIC SENSORS (0/1) ---
        private readonly PluginSensor _isRecordingNum = new("obs_recording_num", "OBS Recording (Numeric)", 0f, "");
        private readonly PluginSensor _isStreamingNum = new("obs_streaming_num", "OBS Streaming (Numeric)", 0f, "");
        private readonly PluginSensor _isReplayBufferNum = new("obs_replay_num", "OBS Replay Buffer (Numeric)", 0f, "");
        private readonly PluginSensor _replayAlertNum = new("obs_replay_alert_num", "Save Status (Numeric)", 0f, "");

        private DateTime _replayAlertEndTime = DateTime.MinValue;
        private bool _isSubscribed = false;

        // v1.1.0: Versioning and instructions added to the plugin description
        public ObsPlugin() : base("obs-monitor-plugin", "OBS Monitor", "v1.1.0 | Displays OBS status. Edit config in 'Documents/InfoPanel/OBS/config.ini'.")
        {
        }

        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

        public override void Initialize()
        {
            LoadPluginConfig();
            TryConnect();
        }

        private void LoadPluginConfig()
        {
            try
            {
                if (string.IsNullOrEmpty(ConfigFilePath)) return;

                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(ConfigFilePath))
                {
                    // Standalone INI parser to extract password
                    var lines = File.ReadAllLines(ConfigFilePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("WebsocketPassword", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("="))
                        {
                            var parts = trimmed.Split(new[] { '=' }, 2);
                            if (parts.Length == 2)
                            {
                                var parsedPassword = parts[1].Trim();
                                if (!string.IsNullOrEmpty(parsedPassword))
                                {
                                    _websocketPassword = parsedPassword;
                                }
                            }
                            break;
                        }
                    }
                }
                else
                {
                    // Create default structure if config.ini doesn't exist yet
                    var defaultSettings = new List<string>
                    {
                        "[OBS_Settings]",
                        $"WebsocketPassword={_websocketPassword}"
                    };
                    File.WriteAllLines(ConfigFilePath, defaultSettings);
                }
            }
            catch (Exception)
            {
                // Fallback to default credentials if file system is completely locked
            }
        }

        private void TryConnect()
        {
            try
            {
                if (!_obs.IsConnected)
                {
#pragma warning disable CS0618
                    _obs.Connect("ws://127.0.0.1:4455", _websocketPassword);
#pragma warning restore CS0618

                    _isSubscribed = false;
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnReplayBufferSaved(object? sender, OBSWebsocketDotNet.Types.Events.ReplayBufferSavedEventArgs e)
        {
            _replayAlertEndTime = DateTime.Now.AddSeconds(5);
        }

        public override void Load(List<IPluginContainer> containers)
        {
            var textContainer = new PluginContainer("obs_text_group", "OBS Studio (ON/OFF)");
            textContainer.Entries.Add(_isRecordingText);
            textContainer.Entries.Add(_isStreamingText);
            textContainer.Entries.Add(_isReplayBufferText);
            textContainer.Entries.Add(_replayAlertText);

            var numContainer = new PluginContainer("obs_num_group", "OBS Studio (0/1)");
            numContainer.Entries.Add(_isRecordingNum);
            numContainer.Entries.Add(_isStreamingNum);
            numContainer.Entries.Add(_isReplayBufferNum);
            numContainer.Entries.Add(_replayAlertNum);

            containers.Add(textContainer);
            containers.Add(numContainer);
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            // 1. Handle Replay Buffer Alerts
            if (DateTime.Now < _replayAlertEndTime)
            {
                _replayAlertText.Value = "SAVED";
                _replayAlertNum.Value = 1f;
            }
            else
            {
                _replayAlertText.Value = "\u2800"; // Braille space configuration to replace standard 0 fallback
                _replayAlertNum.Value = 0f;
            }

            // 2. Connection tracking
            if (!_obs.IsConnected)
            {
                TryConnect();

                _isRecordingText.Value = "OFF";
                _isStreamingText.Value = "OFF";
                _isReplayBufferText.Value = "OFF";

                _isRecordingNum.Value = 0f;
                _isStreamingNum.Value = 0f;
                _isReplayBufferNum.Value = 0f;

                return Task.CompletedTask;
            }

            if (!_isSubscribed)
            {
                try
                {
                    _obs.ReplayBufferSaved -= OnReplayBufferSaved;
                    _obs.ReplayBufferSaved += OnReplayBufferSaved;
                    _isSubscribed = true;
                }
                catch { }
            }

            // 3. Status processing and value serialization (strict 0f / 1f bounds)
            try
            {
                var recordStatus = _obs.GetRecordStatus();
                _isRecordingText.Value = recordStatus.IsRecording ? "ON" : "OFF";
                _isRecordingNum.Value = recordStatus.IsRecording ? 1f : 0f;

                var streamStatus = _obs.GetStreamStatus();
                _isStreamingText.Value = streamStatus.IsActive ? "ON" : "OFF";
                _isStreamingNum.Value = streamStatus.IsActive ? 1f : 0f;

                var replayStatus = _obs.GetReplayBufferStatus();
                _isReplayBufferText.Value = replayStatus ? "ON" : "OFF";
                _isReplayBufferNum.Value = replayStatus ? 1f : 0f;
            }
            catch (Exception)
            {
                _isRecordingText.Value = "OFF";
                _isStreamingText.Value = "OFF";
                _isReplayBufferText.Value = "OFF";

                _isRecordingNum.Value = 0f;
                _isStreamingNum.Value = 0f;
                _isReplayBufferNum.Value = 0f;
            }

            return Task.CompletedTask;
        }

        public override void Update()
        {
        }

        public override void Close()
        {
            if (_obs.IsConnected)
            {
                _obs.ReplayBufferSaved -= OnReplayBufferSaved;
                _obs.Disconnect();
            }
        }
    }

    public class ObsDummyCommand : InfoPanel.Plugins.Host.ICommand
    {
        public string Id => "obs-dummy";
        public string Name => "OBS Dummy";
        public string Description => "Fake command for loader";

        public Task ExecuteAsync(object context)
        {
            return Task.CompletedTask;
        }
    }
}

namespace InfoPanel.Plugins.Host
{
    public interface ICommand
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        Task ExecuteAsync(object context);
    }
}