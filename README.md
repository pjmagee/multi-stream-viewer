# Multi-Stream Viewer

A Blazor WebAssembly application for watching multiple live streams simultaneously from Twitch, YouTube, and Kick.

## Features

- **Multiple Platforms**: Twitch, YouTube, Kick
- **Layout Options**: Grid, Stacked, Horizontal
- **Chat Support**: Side panel or attached to streams
- **URL Sharing**: Load streams directly from URL
- **Responsive Design**: Great with landscape and portrait monitor orienation

## Quick Start

### Prerequisites

- .NET 9 SDK

### Run Locally

```bash
git clone https://github.com/pjmagee/multi-stream-viewer.git
cd multi-stream-viewer/MultiStreamViewer
dotnet run
```

### Live Demo

Visit: `https://pjmagee.github.io/multi-stream-viewer/`

## Usage

### Adding Streams

1. Click **"Manage Streams"**
2. Select platform and enter:
   - **Twitch/Kick**: Channel name (e.g., `shroud`)
   - **YouTube**: Video ID (e.g., `dQw4w9WgXcQ`)
3. Click **"Add Stream"**

### URL Format

```
/{platform}/{identifier}/{platform}/{identifier}/...
```

**Examples:**
```
/twitch/shroud
/twitch/ninja/youtube/dQw4w9WgXcQ/kick/trainwreckstv
```

## Technology

- Blazor WebAssembly (.NET 9)
- Microsoft FluentUI Components
- GitHub Pages deployment

## License

MIT License
