# Multi-Stream Viewer

A standalone Blazor WebAssembly .NET 9 application that enables users to watch multiple streams from different platforms simultaneously using Microsoft FluentUI Blazor components.

## Features

### Supported Platforms
- **Twitch** - Live stream viewing with embedded player
- **Kick** - Live stream viewing with embedded player
- **YouTube** - Live stream viewing with embedded player

### Layout Options
- **Grid Layout** - 2-column responsive grid that automatically adjusts to fit more streams
- **Stacked Layout** - Single column layout, perfect for vertical monitors
- **Horizontal Layout** - Side-by-side layout for wide monitors

### Chat Display Modes
1. **Chat Pane** - Toggleable chat panel on the left or right side with tabs for each stream's chat
2. **Attached Chat** - Each chat is positioned directly next to its corresponding stream

### User Experience Features
- ✅ Easy stream management - Add streams with platform selection and streamer name
- ✅ Quick removal - X button on each stream card
- ✅ URL-based stream loading - Load streams directly from URL path
- ✅ Layout switching - Toggle between grid, stacked, and horizontal layouts
- ✅ Chat management - Toggle chat pane visibility and position
- ✅ Share functionality - Generate shareable URLs with current stream configuration
- ✅ Clear all streams - Remove all streams at once
- ✅ Responsive design - Works on desktop and mobile devices

## Quick Start

### Prerequisites
- .NET 9 SDK
- Modern web browser

### Installation & Running

1. **Clone and navigate to the project:**
   ```bash
   cd MultiStreamViewer
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

3. **Open in browser:**
   Navigate to `http://localhost:5135`

## Usage

### Adding Streams
1. Select a platform (Twitch, YouTube, or Kick) from the dropdown
2. Enter the streamer's username/channel name
3. Click "Add Stream" or press Enter

### URL Loading
Load multiple streams directly via URL:
```
http://localhost:5135/twitch/asmongold/kick/xqc/youtube/destiny
```

Format: `/{platform}/{streamer}/{platform}/{streamer}/...`

### Layout Controls
- **Grid Button** - Switch to responsive grid layout
- **Stacked Button** - Switch to single-column stacked layout  
- **Horizontal Button** - Switch to side-by-side horizontal layout

### Chat Management
- **Pane/Attached Toggle** - Switch between chat pane and attached chat modes
- **Hide/Show Chat** - Toggle chat pane visibility (pane mode only)
- **Chat Position** - Move chat pane to left or right side

### Stream Management
- **X Button** - Remove individual streams
- **Clear All** - Remove all streams at once
- **Share** - Generate shareable URL with current stream configuration

## Technology Stack

- **Frontend Framework**: Blazor WebAssembly (.NET 9)
- **UI Components**: Microsoft FluentUI Blazor
- **Icons**: FluentUI Icons
- **Styling**: CSS with FluentUI Design System
- **State Management**: Singleton service with Observable Collections

## Project Structure

```
MultiStreamViewer/
├── Components/
│   ├── AddStreamForm.razor      # Stream addition form
│   ├── ChatPane.razor           # Chat panel component
│   ├── LayoutControls.razor     # Layout and settings controls
│   ├── StreamCard.razor         # Individual stream display
│   └── StreamsContainer.razor   # Main streams grid container
├── Models/
│   └── StreamInfo.cs           # Stream data models and enums
├── Services/
│   └── StreamService.cs        # Stream management service
├── Pages/
│   └── Home.razor              # Main application page
├── Layout/
│   └── MainLayout.razor        # Application layout
└── wwwroot/
    └── css/
        └── app.css             # Custom styles
```

## Browser Compatibility

- Chrome/Edge (recommended)
- Firefox  
- Safari
- Mobile browsers

## Example URLs

```bash
# Single stream
http://localhost:5135/twitch/shroud

# Multiple streams
http://localhost:5135/twitch/ninja/youtube/mreast/kick/trainwreckstv

# Mixed platforms
http://localhost:5135/twitch/asmongold/kick/xqc/youtube/destiny
```

## Development

### Building
```bash
dotnet build
```

### Running in Development
```bash
dotnet run
```

### Publishing
```bash
dotnet publish -c Release
```

## Deployment

This is a Blazor WebAssembly application that can be deployed to:
- Static web hosting (GitHub Pages, Netlify, Vercel)
- Azure Static Web Apps
- IIS
- Any web server that can serve static files

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is open source and available under the MIT License.
