# InfoPanel OBS Studio Plugin (v1.1.0)

A standalone plugin for [InfoPanel](https://github.com/habibrehmansg/infopanel) that monitors the status of **OBS Studio** via WebSockets.

## Features
- **Text items:** Outputs standard `ON` / `OFF` strings for recording, streaming, and replay buffer.
- **Numeric items:** Outputs `1` / `0` values, perfect for configuring threshold-based image triggers.
- **Replay Buffer Alert:** Temporarily changes state to `SAVED` for 5 seconds when a replay is captured. Uses a Braille space (`\u2800`) as a fallback value to avoid UI shifting.
- **Standalone Configuration:** Automatically generates a `config.ini` file in the user's `Documents/InfoPanel/OBS/` directory for secure and easy WebSocket password setup.

## Installation
1. Download the latest release from GitHub.
2. Import into InfoPanel via the "Import Plugin" feature.
3. Restart InfoPanel.

## Configuration
Upon the first launch, the plugin will create a configuration file at:
`Documents/InfoPanel/OBS/config.ini`
You can simply open it in `Plugins` tab in Infopanel via `Open config` button

Open this file with any text editor and set your OBS WebSocket password:
```ini
[OBS_Settings]
WebsocketPassword=YourActualOBSPassword
