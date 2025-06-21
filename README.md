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

- ✅ **Stream Management Dialog** - Modern dialog interface for adding and managing streams
- ✅ **Platform Selection** - Dropdown with Twitch, YouTube, and Kick support
- ✅ **Bulk Stream Operations** - Select multiple streams for removal
- ✅ **Individual Stream Controls** - Quick removal with X button on each stream card
- ✅ **URL-based Stream Loading** - Load streams directly from URL path segments
- ✅ **Responsive Layout System** - Grid, stacked, and horizontal layout modes
- ✅ **Advanced Chat Management** - Toggleable chat pane with position controls
- ✅ **Share Functionality** - Generate shareable URLs with current configuration
- ✅ **Toast Notifications** - User feedback for actions and errors
- ✅ **Modern FluentUI Design** - Consistent Microsoft design system
- ✅ **Mobile Responsive** - Works seamlessly on desktop and mobile devices

## Quick Start

### Prerequisites
- .NET 9 SDK
- Modern web browser

### Installation & Running

1. **Clone the repository:**
   ```bash
   git clone https://github.com/pjmagee/multi-stream-viewer.git
   cd multi-stream-viewer/MultiStreamViewer
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

3. **Open in browser:**
   Navigate to `http://localhost:5135`

4. **Or access the live version:**
   Visit `https://pjmagee.github.io/multi-stream-viewer/`

## Usage

### Adding Streams

1. Click the **"Manage Streams"** button to open the stream management dialog
2. Select a platform using the icon-based selector (Twitch/YouTube/Kick)
3. Enter the required identifier:
   - **Twitch**: Channel name (e.g., `shroud`)
   - **YouTube**: Video ID (e.g., `dQw4w9WgXcQ`) - **NOT** channel name
   - **Kick**: Channel name (e.g., `trainwreckstv`)
4. Click **"Add Stream"** or press Enter
5. Close the dialog to return to viewing

> **YouTube Note**: YouTube requires the specific Video ID of the live stream, not the channel name. You can find this in the YouTube URL: `youtube.com/watch?v=VIDEO_ID`

### Managing Existing Streams

1. Open the **"Manage Streams"** dialog
2. Use checkboxes to select multiple streams for bulk operations
3. Click the **X button** on individual streams for quick removal
4. Use **"Remove Selected"** for bulk removal

### URL Loading

Load multiple streams directly via URL:

```url
http://localhost:5135/twitch/asmongold/kick/xqc/youtube/dQw4w9WgXcQ
```

Format: `/{platform}/{identifier}/{platform}/{identifier}/...`

**Important**: 
- **Twitch/Kick**: Use channel names
- **YouTube**: Use video IDs (the part after `v=` in YouTube URLs)

### Layout Controls

- **Grid Button** - Switch to responsive 2-column grid layout
- **Stacked Button** - Switch to single-column vertical layout  
- **Horizontal Button** - Switch to side-by-side horizontal layout

### Chat Management

- **Pane/Attached Toggle** - Switch between chat pane and attached chat modes
- **Hide/Show Chat** - Toggle chat pane visibility (pane mode only)
- **Left/Right Position** - Move chat pane to left or right side of screen

### Additional Features

- **Share Button** - Generate shareable URL with current stream configuration
- **Toast Notifications** - Receive feedback for successful actions and errors
- **Responsive Design** - Automatically adapts to screen size and orientation

## Technology Stack

- **Frontend Framework**: Blazor WebAssembly (.NET 9)
- **UI Library**: Microsoft FluentUI Blazor Components
- **Icons**: FluentUI Icons System
- **Styling**: Component-scoped CSS with FluentUI Design Tokens
- **State Management**: Singleton Service with ObservableCollection
- **Deployment**: GitHub Pages with GitHub Actions
- **Compression**: Client-side Brotli decompression
- **PWA Features**: Service Worker ready with manifest.json

## Project Structure

```
MultiStreamViewer/
├── Components/
│   ├── ChatPane.razor                    # Chat panel component with tabs
│   ├── ChatPane.razor.css               # Chat pane specific styles
│   ├── ManageStreamsDialog.razor        # Stream management dialog
│   ├── ManageStreamsDialog.razor.css    # Dialog specific styles
│   ├── StreamCard.razor                 # Individual stream display card
│   ├── StreamCard.razor.css             # Stream card specific styles
│   ├── StreamsContainer.razor           # Main streams container with layouts
│   └── StreamsContainer.razor.css       # Container layout styles
├── Models/
│   └── StreamInfo.cs                    # Stream data models, enums, and platform configs
├── Services/
│   └── StreamService.cs                 # Stream state management service
├── Pages/
│   └── Home.razor                       # Main application page with controls
├── Layout/
│   ├── MainLayout.razor                 # Application shell layout
│   └── MainLayout.razor.css             # Layout specific styles
├── wwwroot/
│   ├── css/
│   │   └── app.css                      # Global application styles
│   ├── js/
│   │   └── decode.js                    # Brotli decompression for GitHub Pages
│   ├── .nojekyll                        # GitHub Pages configuration
│   ├── 404.html                         # SPA routing support for GitHub Pages
│   ├── manifest.json                    # PWA manifest
│   ├── robots.txt                       # SEO crawling instructions
│   └── sitemap.xml                      # SEO sitemap
└── .github/
    └── workflows/
        └── main.yml                     # GitHub Pages deployment workflow
```

## Browser Compatibility

- Chrome/Edge (recommended)
- Firefox  
- Safari
- Mobile browsers

## Example URLs

```bash
# Single stream
https://pjmagee.github.io/multi-stream-viewer/twitch/shroud

# Multiple streams (NOTE: YouTube examples use video IDs, not channel names)
https://pjmagee.github.io/multi-stream-viewer/twitch/ninja/youtube/dQw4w9WgXcQ/kick/trainwreckstv

# Mixed platforms example
https://pjmagee.github.io/multi-stream-viewer/twitch/asmongold/kick/xqc/youtube/jNQXAC9IVRw

# Local development
http://localhost:5135/twitch/asmongold/kick/xqc/youtube/jNQXAC9IVRw
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

This is a Blazor WebAssembly application optimized for **GitHub Pages** deployment with:

- ✅ **Automated GitHub Actions workflow** - Deploys on push to main branch
- ✅ **Brotli compression support** - Client-side decompression for faster loading  
- ✅ **SPA routing support** - Proper handling of client-side routes
- ✅ **PWA capabilities** - Manifest and service worker ready
- ✅ **SEO optimization** - Meta tags, sitemap, and robots.txt included

### GitHub Pages Setup

1. **Enable GitHub Pages** in repository settings
2. **Set source** to "GitHub Actions" 
3. **Push to main branch** - Deployment happens automatically
4. **Access your app** at `https://[username].github.io/multi-stream-viewer/`

The included GitHub Actions workflow handles:
- .NET 9 SDK setup
- Application publishing  
- Base href rewriting for GitHub Pages
- Static asset deployment

### Alternative Deployment Options

While optimized for GitHub Pages, this application can also be deployed to:

- Azure Static Web Apps
- Netlify  
- Vercel
- Any static web hosting service

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is open source and available under the MIT License.
