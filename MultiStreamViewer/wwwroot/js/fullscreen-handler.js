// Fullscreen detection handler
let dotNetHelper = null;

export function initializeFullscreenHandler(dotNetReference) {
    dotNetHelper = dotNetReference;
    
    console.log('Fullscreen handler initialized');
    
    // Listen for fullscreen changes (Fullscreen API only)
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange); // Safari
    document.addEventListener('mozfullscreenchange', handleFullscreenChange); // Firefox
    document.addEventListener('MSFullscreenChange', handleFullscreenChange); // IE/Edge
    
    // Check initial state
    handleFullscreenChange();
}

function handleFullscreenChange() {
    const isFullscreen = !!(
        document.fullscreenElement ||
        document.webkitFullscreenElement ||
        document.mozFullScreenElement ||
        document.msFullscreenElement
    );
    
    console.log('Fullscreen state changed:', isFullscreen);
    
    if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync('OnFullscreenChanged', isFullscreen);
    }
}

export async function toggleFullscreen() {
    if (!document.fullscreenElement) {
        // Enter fullscreen
        try {
            await document.documentElement.requestFullscreen();
            return true;
        } catch (err) {
            console.error('Error attempting to enable fullscreen:', err);
            return false;
        }
    } else {
        // Exit fullscreen
        try {
            await document.exitFullscreen();
            return true;
        } catch (err) {
            console.error('Error attempting to exit fullscreen:', err);
            return false;
        }
    }
}

export function cleanupFullscreenHandler() {
    document.removeEventListener('fullscreenchange', handleFullscreenChange);
    document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
    document.removeEventListener('mozfullscreenchange', handleFullscreenChange);
    document.removeEventListener('MSFullscreenChange', handleFullscreenChange);
    dotNetHelper = null;
}
