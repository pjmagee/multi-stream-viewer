let twitchScriptPromise;
const activeEmbeds = new Map();

function ensureTwitchScript() {
    if (window.Twitch && typeof window.Twitch.Embed === "function") {
        return Promise.resolve();
    }

    if (twitchScriptPromise) {
        return twitchScriptPromise;
    }

    twitchScriptPromise = new Promise((resolve, reject) => {
        const existing = document.querySelector("script[data-twitch-embed='true']");
        if (existing) {
            existing.addEventListener("load", () => resolve(), { once: true });
            existing.addEventListener("error", () => reject(new Error("Failed to load Twitch embed script.")), { once: true });
            return;
        }

        const script = document.createElement("script");
        script.src = "https://embed.twitch.tv/embed/v1.js";
        script.async = true;
        script.dataset.twitchEmbed = "true";
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("Failed to load Twitch embed script."));
        document.head.appendChild(script);
    });

    return twitchScriptPromise;
}

export async function initializeTwitchEmbed(elementId, channel, parentHosts) {
    if (!elementId || !channel) {
        return;
    }

    const host = document.getElementById(elementId);
    if (!host) {
        return;
    }

    await ensureTwitchScript();

    disposeTwitchEmbed(elementId);

    const normalizedParents = Array.isArray(parentHosts) && parentHosts.length > 0
        ? parentHosts
        : [window.location.hostname];

    const embed = new window.Twitch.Embed(elementId, {
        width: "100%",
        height: "100%",
        channel,
        parent: normalizedParents,
        layout: "video",
        autoplay: false,
        muted: true
    });

    activeEmbeds.set(elementId, embed);
}

export function disposeTwitchEmbed(elementId) {
    const host = document.getElementById(elementId);
    activeEmbeds.delete(elementId);

    if (host) {
        host.innerHTML = "";
    }
}