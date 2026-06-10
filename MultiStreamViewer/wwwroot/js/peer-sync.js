// Real-time "watch together" sync over PeerJS (WebRTC).
// The app stays fully static: PeerJS's free public cloud broker is used only
// for the initial handshake, after which control messages flow directly
// peer-to-peer. Topology is a star through the host, so any participant's
// change reaches everyone (the host relays guest messages to other guests).

import * as PeerJS from "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/+esm";

const Peer = PeerJS.Peer || PeerJS.default;

let peer = null;
let isHost = false;
let dotNet = null;
const connections = new Map(); // remotePeerId -> DataConnection

export function startSession(dotNetRef) {
    dotNet = dotNetRef;
    isHost = true;
    return createPeer();
}

export async function joinSession(hostId, dotNetRef) {
    dotNet = dotNetRef;
    isHost = false;
    const id = await createPeer();
    wireConnection(peer.connect(hostId, { reliable: true }));
    return id;
}

export function broadcast(json) {
    for (const conn of connections.values()) {
        if (conn.open) {
            conn.send(json);
        }
    }
}

export function leave() {
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
}

function createPeer() {
    return new Promise((resolve, reject) => {
        peer = new Peer();

        peer.on("open", (id) => {
            if (isHost) {
                peer.on("connection", (conn) => wireConnection(conn));
            }
            resolve(id);
        });

        peer.on("error", (err) => {
            const type = err && err.type ? err.type : String(err);
            if (dotNet) {
                dotNet.invokeMethodAsync("OnSyncError", type);
            }
            reject(err);
        });
    });
}

function wireConnection(conn) {
    conn.on("open", () => {
        connections.set(conn.peer, conn);
        notifyPeers();

        // Host brings a newly-joined guest up to date with the current state.
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
        dotNet.invokeMethodAsync("OnSnapshotReceived", json);
    }
}

function notifyPeers() {
    if (dotNet) {
        dotNet.invokeMethodAsync("OnPeerCountChanged", connections.size);
    }
}
