// YouTube playback sync via the IFrame Player API — client-side, no Data API key.
//
// Reliability model:
//  - Every message carries a per-stream VERSION. A newer version applies fully
//    (toggle play/pause + seek); the SAME version (the host's drift heartbeat)
//    only nudges time; an OLDER version is ignored. So a heartbeat can never
//    undo a more recent action — that was the main source of jitter.
//  - play/pause are verified after a short delay and retried, so a command that
//    lands during BUFFERING isn't dropped. If a play is autoplay-blocked we mute
//    and retry, so a freshly-joined guest still starts in sync (then can unmute).
//  - A light poll detects seeks (including while paused), which fire no event.

let apiReady = null;
let dotNet = null;
let pollHandle = null;
const players = new Map(); // elementId -> entry

const SEEK_THRESHOLD = 2.0;   // poll: a jump larger than this is treated as a seek
const DRIFT_THRESHOLD = 1.5;  // same-version heartbeat: only re-seek beyond this
const SUPPRESS_MS = 1500;     // ignore self-induced events for this long
const VERIFY_MS = 500;        // re-check play/pause took effect after this
const POLL_MS = 500;

export async function register(elementId, streamId, dotNetRef) {
    dotNet = dotNetRef;
    await loadApi();

    if (players.has(elementId) || !document.getElementById(elementId)) {
        return;
    }

    const entry = {
        player: null,
        streamId,
        ready: false,
        synced: false,
        version: 0,
        suppressUntil: 0,
        lastTime: 0,
        lastWall: performance.now(),
        wasPlaying: false,
    };

    entry.player = new YT.Player(elementId, {
        events: {
            onReady: () => { entry.ready = true; entry.lastTime = safeTime(entry.player); },
            onStateChange: (e) => onStateChange(entry, e),
        },
    });

    players.set(elementId, entry);
    ensurePolling();
}

export function unregister(elementId) {
    const entry = players.get(elementId);
    if (entry) {
        try { entry.player.destroy(); } catch { /* gone */ }
        players.delete(elementId);
    }
    if (players.size === 0 && pollHandle) {
        clearInterval(pollHandle);
        pollHandle = null;
    }
}

// Apply a peer's message (action or heartbeat) using version ordering.
export function applyMessage(streamId, playing, time, version) {
    const entry = findByStream(streamId);
    if (!entry || !entry.ready) {
        return;
    }

    // First message after joining: snap to it regardless of version.
    if (!entry.synced) {
        entry.synced = true;
        entry.version = version;
        seekAndToggle(entry, playing, time);
        return;
    }

    if (version < entry.version) {
        return; // stale — a newer action already won
    }

    const sameVersion = version === entry.version;
    entry.version = version;

    if (sameVersion) {
        // Drift heartbeat: correct time only, never toggle play/pause.
        entry.suppressUntil = performance.now() + SUPPRESS_MS;
        try {
            const cur = safeTime(entry.player);
            if (playing && entry.player.getPlayerState() === YT.PlayerState.PLAYING
                && Math.abs(cur - time) > DRIFT_THRESHOLD) {
                entry.player.seekTo(time, true);
            }
        } catch { /* not ready */ }
        return;
    }

    // Newer action: full apply.
    seekAndToggle(entry, playing, time);
}

// Snapshot of every local player's state (the host's drift heartbeat).
export function getStates() {
    const out = [];
    for (const entry of players.values()) {
        if (!entry.ready) {
            continue;
        }
        try {
            out.push({
                streamId: entry.streamId,
                playing: entry.player.getPlayerState() === YT.PlayerState.PLAYING,
                time: safeTime(entry.player),
                version: entry.version,
            });
        } catch { /* skip */ }
    }
    return out;
}

function seekAndToggle(entry, playing, time) {
    entry.suppressUntil = performance.now() + SUPPRESS_MS;
    try {
        const cur = safeTime(entry.player);
        if (Math.abs(cur - time) > 1.0) {
            entry.player.seekTo(time, true);
        }
        if (playing) {
            ensurePlaying(entry);
        } else {
            ensurePaused(entry);
        }
        entry.lastTime = time;
        entry.lastWall = performance.now();
        entry.wasPlaying = playing;
    } catch { /* not ready */ }
}

function ensurePlaying(entry) {
    try { entry.player.playVideo(); } catch { /* noop */ }
    setTimeout(() => {
        try {
            const state = entry.player.getPlayerState();
            if (state !== YT.PlayerState.PLAYING && state !== YT.PlayerState.BUFFERING) {
                // Autoplay was blocked (fresh tab) — mute so it can start in sync.
                try { entry.player.mute(); } catch { /* noop */ }
                entry.player.playVideo();
            }
        } catch { /* noop */ }
    }, VERIFY_MS);
}

function ensurePaused(entry) {
    try { entry.player.pauseVideo(); } catch { /* noop */ }
    setTimeout(() => {
        try {
            if (entry.player.getPlayerState() === YT.PlayerState.PLAYING) {
                entry.player.pauseVideo();
            }
        } catch { /* noop */ }
    }, VERIFY_MS);
}

function onStateChange(entry, event) {
    if (!dotNet || !window.YT) {
        return;
    }
    if (performance.now() < entry.suppressUntil) {
        return; // self-induced — don't echo
    }

    const state = event.data;
    if (state === YT.PlayerState.PLAYING || state === YT.PlayerState.PAUSED) {
        reportLocal(entry, state === YT.PlayerState.PLAYING);
    }
}

function reportLocal(entry, playing) {
    entry.version += 1;
    entry.synced = true;
    const time = safeTime(entry.player);
    entry.lastTime = time;
    entry.lastWall = performance.now();
    entry.wasPlaying = playing;
    dotNet.invokeMethodAsync("OnYouTubePlayback", entry.streamId, playing, time, entry.version);
}

function ensurePolling() {
    if (!pollHandle) {
        pollHandle = setInterval(pollSeeks, POLL_MS);
    }
}

// Detect seeks (including while paused), which fire no onStateChange event.
function pollSeeks() {
    if (!dotNet) {
        return;
    }

    const now = performance.now();
    for (const entry of players.values()) {
        if (!entry.ready || now < entry.suppressUntil) {
            if (entry.ready) {
                entry.lastTime = safeTime(entry.player);
                entry.lastWall = now;
            }
            continue;
        }

        let state;
        let cur;
        try {
            state = entry.player.getPlayerState();
            cur = safeTime(entry.player);
        } catch {
            continue;
        }

        // Buffering makes time lag; don't mistake it for a seek.
        if (state === YT.PlayerState.BUFFERING || state === YT.PlayerState.UNSTARTED) {
            entry.lastTime = cur;
            entry.lastWall = now;
            continue;
        }

        const expected = entry.lastTime + (entry.wasPlaying ? (now - entry.lastWall) / 1000 : 0);
        if (Math.abs(cur - expected) > SEEK_THRESHOLD) {
            reportLocal(entry, state === YT.PlayerState.PLAYING);
        } else {
            entry.lastTime = cur;
            entry.lastWall = now;
            entry.wasPlaying = state === YT.PlayerState.PLAYING;
        }
    }
}

function findByStream(streamId) {
    for (const entry of players.values()) {
        if (entry.streamId === streamId) {
            return entry;
        }
    }
    return null;
}

function safeTime(player) {
    try { return player.getCurrentTime() || 0; } catch { return 0; }
}

function loadApi() {
    if (apiReady) {
        return apiReady;
    }

    apiReady = new Promise((resolve) => {
        if (window.YT && window.YT.Player) {
            resolve();
            return;
        }

        const previous = window.onYouTubeIframeAPIReady;
        window.onYouTubeIframeAPIReady = () => {
            if (typeof previous === "function") {
                previous();
            }
            resolve();
        };

        if (!document.querySelector("script[data-yt-api]")) {
            const script = document.createElement("script");
            script.src = "https://www.youtube.com/iframe_api";
            script.dataset.ytApi = "true";
            document.head.appendChild(script);
        }
    });

    return apiReady;
}
