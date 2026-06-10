// Opens a stream in its own browser window so it keeps playing when the app
// tab is hidden. Per-stream audio is handled by each embed's own controls.

export function popOutStream(url, name) {
    const features = "width=960,height=540,menubar=no,toolbar=no,location=no,status=no,resizable=yes";
    window.open(url, `msv_popout_${name}`, features);
}
