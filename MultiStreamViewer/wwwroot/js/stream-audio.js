// Per-stream audio control for non-Twitch embeds, plus "pop out to a window".
// Twitch is handled separately in twitch-embed.js via its player API.

// YouTube IFrame API accepts postMessage commands when the embed URL carries
// enablejsapi=1. The solo button click is the user gesture that lets us unmute.
export function setYouTubeMuted(elementId, muted) {
    const iframe = document.getElementById(elementId);
    if (!iframe || !iframe.contentWindow) {
        return;
    }

    post(iframe, muted ? "mute" : "unMute");

    if (!muted) {
        // Ensure it's actually playing so there's something to hear.
        post(iframe, "playVideo");
    }
}

// Kick's player exposes no documented runtime audio API, so the only reliable
// way to mute/unmute is to reload the iframe with the desired query string.
// This only happens when a Kick tile crosses the mute<->unmute boundary
// (i.e. when it gains or loses solo), not on every audio switch.
export function setKickMuted(elementId, muted) {
    const iframe = document.getElementById(elementId);
    if (!iframe) {
        return;
    }

    const base = iframe.src.split("?")[0];
    const next = muted
        ? `${base}?autoplay=false&muted=true`
        : `${base}?autoplay=true&muted=false`;

    if (iframe.src !== next) {
        iframe.src = next;
    }
}

// Best-effort fallback for any other iframe embed (tries the YouTube command
// protocol; a no-op for players that don't understand it).
export function setIframeMuted(elementId, muted) {
    const iframe = document.getElementById(elementId);
    if (!iframe || !iframe.contentWindow) {
        return;
    }

    post(iframe, muted ? "mute" : "unMute");
}

export function popOutStream(url, name) {
    const features = "width=960,height=540,menubar=no,toolbar=no,location=no,status=no,resizable=yes";
    window.open(url, `msv_popout_${name}`, features);
}

function post(iframe, func) {
    iframe.contentWindow.postMessage(
        JSON.stringify({ event: "command", func, args: [] }),
        "*");
}
