# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-06-10

### Added

- Per-card overlay controls (top-left of each stream): remove, replace, pop-out,
  and a drag handle.
- Remove a stream in place — the grid automatically reflows the remaining streams.
- Replace a stream's channel via a flip-to-edit form (accepts a full URL or the
  `t/`, `y/`, `k/` shorthand).
- Pop-out a stream into its own browser window so it keeps playing when the app
  tab is hidden.
- Drag-to-reorder streams within the grid. Reordering uses CSS `order` over a
  stable DOM, so tiles rearrange without reloading their embeds.
- Watch-together sessions: real-time sync over PeerJS (no backend). Adding,
  removing, replacing, and reordering streams stays in sync across all
  participants. Collaborative — anyone can drive — via a star topology through
  the host. Joinable by shareable link.
- Sessions persist across a page reload via `localStorage`: hosts reclaim their
  previous peer id and guests redial the host with bounded reconnection retries.
- YouTube playback sync for watch-together: play, pause, and seek of YouTube
  videos sync across participants via the IFrame Player API (no Data API key).
  Version-ordered messages and a host drift heartbeat keep clients aligned;
  a freshly-joined guest auto-starts (muted) in sync.

### Changed

- Per-stream audio is now controlled by each embed's own player controls.

## [0.1.0] - 2026-06-06

### Added

- Initial release: watch multiple live streams from Twitch, YouTube, and Kick
  simultaneously in a responsive grid.
- 16:9 layout optimizer that maximizes stream size for the available space.
- Chat in a side pane (left/right) or attached beside each stream.
- Add streams via full URLs or shorthand patterns, and share a layout by URL.
- Fullscreen mode and a manage-streams dialog.

[Unreleased]: https://github.com/pjmagee/multi-stream-viewer/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/pjmagee/multi-stream-viewer/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/pjmagee/multi-stream-viewer/releases/tag/v0.1.0
