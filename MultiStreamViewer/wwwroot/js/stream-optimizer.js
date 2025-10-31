// Stream layout optimization based on multitwitch.tv approach
// Maintains 16:9 aspect ratio while maximizing use of available space

window.streamOptimizer = {
    resizeObserver: null,
    resizeTimeout: null,

    optimizeStreamLayout: function (streamCount, containerId = 'streams-container') {
        if (streamCount <= 0) return;

        const container = document.getElementById(containerId) || document.querySelector('.streams-container');
        if (!container) {
            console.warn('Stream container not found');
            return;
        }

        // Get available dimensions
        const containerRect = container.getBoundingClientRect();
        let height = containerRect.height;
        let width = containerRect.width;

        // Account for container padding (1px on streams-container)
        const containerStyle = window.getComputedStyle(container);
        const containerPaddingTop = parseFloat(containerStyle.paddingTop) || 0;
        const containerPaddingBottom = parseFloat(containerStyle.paddingBottom) || 0;
        const containerPaddingLeft = parseFloat(containerStyle.paddingLeft) || 0;
        const containerPaddingRight = parseFloat(containerStyle.paddingRight) || 0;

        height = height - containerPaddingTop - containerPaddingBottom;
        width = width - containerPaddingLeft - containerPaddingRight;

        console.log(`Container dimensions (after padding): ${width}x${height}`);

        if (height <= 0 || width <= 0) return;

        let bestHeight = 0;
        let bestWidth = 0;
        let bestColumns = 1;
        let bestRows = 1;

        const gap = 8; // Gap between grid items

        // Try different column arrangements to find optimal size
        for (let columns = 1; columns <= streamCount; columns++) {
            const rows = Math.ceil(streamCount / columns);
            
            // Calculate total gap space
            const totalHorizontalGap = (columns - 1) * gap;
            const totalVerticalGap = (rows - 1) * gap;
            
            // Calculate maximum dimensions per stream (accounting for gaps)
            // Use floor to ensure we never exceed container dimensions
            let maxWidth = Math.floor((width - totalHorizontalGap) / columns);
            let maxHeight = Math.floor((height - totalVerticalGap) / rows);

            // Maintain 16:9 aspect ratio (Twitch standard)
            if (maxWidth * 9 / 16 < maxHeight) {
                // Width is the limiting factor
                maxHeight = Math.floor(maxWidth * 9 / 16);
            } else {
                // Height is the limiting factor
                maxWidth = Math.floor(maxHeight * 16 / 9);
            }

            // Check if this arrangement gives us larger streams
            if (maxWidth > bestWidth) {
                bestWidth = maxWidth;
                bestHeight = maxHeight;
                bestColumns = columns;
                bestRows = rows;
            }
        }
        
        console.log(`Best layout: ${bestColumns} columns x ${bestRows} rows`);
        console.log(`Stream size: ${bestWidth}x${bestHeight}`);
        console.log(`Total width needed: ${bestWidth * bestColumns + (bestColumns - 1) * gap}`);
        console.log(`Total height needed: ${bestHeight * bestRows + (bestRows - 1) * gap}`);
        console.log(`Available: ${width}x${height}`);
        
        // Apply the optimal dimensions to all stream cards
        const streamCards = container.querySelectorAll('.stream-card');
        const stackItems = container.querySelectorAll('.stack-item');

        // Calculate flex basis as percentage for equal distribution
        const flexBasis = Math.floor(100 / bestColumns);

        streamCards.forEach(card => {
            card.style.height = bestHeight + 'px';
            card.style.minHeight = bestHeight + 'px';
            card.style.maxHeight = bestHeight + 'px';
            // Remove fixed width to allow flexbox to control width
            card.style.width = '';
            card.style.minWidth = '';
        });

        stackItems.forEach(item => {
            // Use exact pixel width to avoid rounding issues
            item.style.flex = `0 0 ${bestWidth}px`;
            item.style.height = bestHeight + 'px';
            item.style.minHeight = bestHeight + 'px';
            item.style.maxHeight = bestHeight + 'px';
            // Remove fixed width to allow flexbox to control width
            item.style.width = '';
        });

        // Apply iframe dimensions to maintain aspect ratio
        const iframes = container.querySelectorAll('.stream-iframe');
        iframes.forEach(iframe => {
            iframe.style.width = '100%';
            iframe.style.height = '100%';
        });

        console.log(`Optimized layout: ${bestColumns}x${bestRows}, size: ${Math.floor(bestWidth)}x${Math.floor(bestHeight)}`);

        return {
            width: Math.floor(bestWidth),
            height: Math.floor(bestHeight),
            columns: bestColumns,
            rows: bestRows
        };
    },

    // Initialize optimization with resize listener
    initialize: function () {
        console.log('Initializing stream optimizer');
        
        // Clean up existing observer if any
        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
        }

        // Window resize listener
        window.addEventListener('resize', () => {
            clearTimeout(this.resizeTimeout);
            this.resizeTimeout = setTimeout(() => {
                console.log('Window resized, re-optimizing layout');
                const streamCount = document.querySelectorAll('.stream-card').length;
                if (streamCount > 0) {
                    this.optimizeStreamLayout(streamCount);
                }
            }, 100);
        });

        // ResizeObserver to detect container size changes (including when moved between monitors)
        const container = document.getElementById('streams-container') || document.querySelector('.streams-container');
        if (container && typeof ResizeObserver !== 'undefined') {
            console.log('Setting up ResizeObserver on container');
            this.resizeObserver = new ResizeObserver((entries) => {
                clearTimeout(this.resizeTimeout);
                this.resizeTimeout = setTimeout(() => {
                    console.log('Container resized, re-optimizing layout');
                    const streamCount = document.querySelectorAll('.stream-card').length;
                    if (streamCount > 0) {
                        this.optimizeStreamLayout(streamCount);
                    }
                }, 100);
            });
            
            this.resizeObserver.observe(container);
            console.log('ResizeObserver attached to container');
        } else {
            console.warn('ResizeObserver not available or container not found');
        }
    }
};

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.streamOptimizer.initialize();
});

// Re-initialize when called (for Blazor scenarios)
window.streamOptimizer.ensureInitialized = function() {
    const container = document.getElementById('streams-container') || document.querySelector('.streams-container');
    if (container && !this.resizeObserver) {
        console.log('Re-initializing stream optimizer');
        this.initialize();
    }
};