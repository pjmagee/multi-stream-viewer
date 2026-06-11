// Real-time "watch together" sync over PeerJS (WebRTC).
// The app stays fully static: PeerJS's free public cloud broker is used only
// for the initial handshake, after which control messages flow directly
// peer-to-peer. Topology is a star through the host, so any participant's
// change reaches everyone (the host relays guest messages to other guests).
//
// Sessions survive a page refresh: the host reclaims its previous peer id and
// guests redial the same host (with bounded retries), so reloading the tab
// rejoins the same room rather than dropping out.

import * as PeerJS from "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/+esm";

const Peer = PeerJS.Peer || PeerJS.default;

const RECONNECT_INTERVAL_MS = 2000;
const MAX_RECONNECT_ATTEMPTS = 15; // ~30s before giving up on a dead room
const MAX_RECLAIM_ATTEMPTS = 8;    // host reclaiming its id after a refresh

let peer = null;
let isHost = false;
let dotNet = null;
let hostId = null;
let intentionalLeave = false;
let reconnectTimer = null;
let reconnectAttempts = 0;
const connections = new Map(); // remotePeerId -> DataConnection

export async function startSession(dotNetRef, preferredId) {
    dotNet = dotNetRef;
    isHost = true;
    intentionalLeave = false;
    return createPeer(preferredId || undefined);
}

export async function joinSession(host, dotNetRef) {
    dotNet = dotNetRef;
    isHost = false;
    hostId = host;
    intentionalLeave = false;
    reconnectAttempts = 0;
    const id = await createPeer();
    attemptConnect();
    return id;
}

export function broadcast(json) {
    for (const conn of connections.values()) {
        if (conn.open) {
            conn.send(json);
        }
    }
}

// Quick reachability check for a saved session's host id, used when relaunching
// a session from history. Resolves true if a data channel to the host opens
// within the timeout, false if the broker reports the id is unregistered (the
// room is gone) or we time out. Uses a throwaway peer so it never disturbs an
// active session, and tears it down on either outcome.
export function probeHost(hostId, timeoutMs) {
    return new Promise((resolve) => {
        let settled = false;
        let probe;

        const finish = (reachable) => {
            if (settled) {
                return;
            }
            settled = true;
            clearTimeout(timer);
            try { if (probe) probe.destroy(); } catch { /* already gone */ }
            resolve(reachable);
        };

        const timer = setTimeout(() => finish(false), timeoutMs || 5000);

        try {
            probe = new Peer();
        } catch {
            finish(false);
            return;
        }

        probe.on("open", () => {
            const conn = probe.connect(hostId, { reliable: true });
            if (!conn) {
                finish(false);
                return;
            }
            conn.on("open", () => finish(true));
            conn.on("error", () => finish(false));
        });

        probe.on("error", (err) => {
            const type = err && err.type ? err.type : String(err);
            // peer-unavailable => the host id isn't registered => room is gone.
            if (type === "peer-unavailable") {
                finish(false);
            }
        });
    });
}

export function leave() {
    intentionalLeave = true;
    if (reconnectTimer) {
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
    }
    for (const conn of connections.values()) {
        try { conn.close(); } catch { /* already closing */ }
    }
    connections.clear();
    if (peer) {
        try { peer.destroy(); } catch { /* already destroyed */ }
        peer = null;
    }
    dotNet = null;
    isHost = false;
    hostId = null;
}

function createPeer(preferredId, reclaimAttempt = 0) {
    return new Promise((resolve, reject) => {
        peer = preferredId ? new Peer(preferredId) : new Peer();
        let opened = false;

        peer.on("open", (id) => {
            opened = true;
            if (isHost) {
                peer.on("connection", (conn) => wireConnection(conn));
            }
            resolve(id);
        });

        peer.on("error", (err) => {
            const type = err && err.type ? err.type : String(err);

            // A host reclaiming its own id moments after a refresh may briefly see
            // it as still taken by the old (now-dead) connection — retry a few times.
            if (!opened && type === "unavailable-id" && preferredId && reclaimAttempt < MAX_RECLAIM_ATTEMPTS) {
                try { peer.destroy(); } catch { /* noop */ }
                setTimeout(
                    () => createPeer(preferredId, reclaimAttempt + 1).then(resolve, reject),
                    800);
                return;
            }

            if (dotNet) {
                dotNet.invokeMethodAsync("OnSyncError", type);
            }

            // Host not reachable yet (e.g. mid-refresh) — keep retrying the dial.
            if (opened && !isHost && type === "peer-unavailable") {
                scheduleReconnect();
            }

            if (!opened) {
                reject(err);
            }
        });
    });
}

function attemptConnect() {
    if (intentionalLeave || isHost || connections.size > 0) {
        return;
    }
    if (peer && !peer.destroyed && hostId) {
        wireConnection(peer.connect(hostId, { reliable: true }));
    }
}

function scheduleReconnect() {
    if (intentionalLeave || isHost || connections.size > 0 || reconnectTimer) {
        return;
    }

    reconnectAttempts += 1;
    if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS) {
        if (dotNet) {
            dotNet.invokeMethodAsync("OnReconnectFailed");
        }
        return;
    }

    reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        attemptConnect();
        scheduleReconnect();
    }, RECONNECT_INTERVAL_MS);
}

function wireConnection(conn) {
    conn.on("open", () => {
        connections.set(conn.peer, conn);
        reconnectAttempts = 0;
        notifyPeers();

        // Host brings a newly-(re)joined guest up to date with the current state.
        if (isHost && dotNet) {
            dotNet.invokeMethodAsync("GetCurrentSnapshot").then((json) => {
                if (json && conn.open) {
                    conn.send(json);
                }
            });
        }
    });

    conn.on("data", (data) => handleData(conn, data));

    const drop = () => {
        connections.delete(conn.peer);
        notifyPeers();
        // A guest that lost its link to the host keeps trying to get back in.
        if (!isHost && !intentionalLeave) {
            scheduleReconnect();
        }
    };
    conn.on("close", drop);
    conn.on("error", drop);
}

function handleData(sourceConn, data) {
    const json = typeof data === "string" ? data : JSON.stringify(data);

    // Star relay: forward each guest's update to all other guests.
    if (isHost) {
        for (const [remoteId, conn] of connections) {
            if (remoteId !== sourceConn.peer && conn.open) {
                conn.send(json);
            }
        }
    }

    if (dotNet) {
        dotNet.invokeMethodAsync("OnMessageReceived", json);
    }
}

function notifyPeers() {
    if (dotNet) {
        dotNet.invokeMethodAsync("OnPeerCountChanged", connections.size);
    }
}
