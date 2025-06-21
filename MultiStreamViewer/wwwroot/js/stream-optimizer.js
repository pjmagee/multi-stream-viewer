// Stream layout optimization based on multitwitch.tv approach
// Maintains 16:9 aspect ratio while maximizing use of available space

window.streamOptimizer = {
    optimizeStreamLayout: function(streamCount, containerId = 'streams-container') {
        if (streamCount <= 0) return;
        
        const container = document.getElementById(containerId) || document.querySelector('.streams-container');
        if (!container) return;
        
        // Get available dimensions
        const containerRect = container.getBoundingClientRect();
        const height = containerRect.height;
        const width = containerRect.width;
        
        if (height <= 0 || width <= 0) return;
        
        let bestHeight = 0;
        let bestWidth = 0;
        let bestColumns = 1;
        let bestRows = 1;
        
        // Try different column arrangements to find optimal size
        for (let columns = 1; columns <= streamCount; columns++) {
            const rows = Math.ceil(streamCount / columns);
              // Calculate maximum dimensions per stream
            let maxWidth = Math.floor(width / columns) - 8; // Account for smaller gaps
            let maxHeight = Math.floor(height / rows) - 8; // Account for smaller gaps
            
            // Maintain 16:9 aspect ratio (Twitch standard)
            if (maxWidth * 9/16 < maxHeight) {
                // Width is the limiting factor
                maxHeight = maxWidth * 9/16;
            } else {
                // Height is the limiting factor
                maxWidth = maxHeight * 16/9;
            }
            
            // Check if this arrangement gives us larger streams
            if (maxWidth > bestWidth) {
                bestWidth = maxWidth;
                bestHeight = maxHeight;
                bestColumns = columns;
                bestRows = rows;
            }
        }
          // Apply the optimal dimensions to all stream cards
        const streamCards = container.querySelectorAll('.stream-card');
        const stackItems = container.querySelectorAll('.stack-item');
        
        // Calculate flex basis as percentage for equal distribution
        const flexBasis = Math.floor(100 / bestColumns);
        
        streamCards.forEach(card => {
            card.style.height = Math.floor(bestHeight) + 'px';
            card.style.minHeight = Math.floor(bestHeight) + 'px';
            // Remove fixed width to allow flexbox to control width
            card.style.width = '';
            card.style.minWidth = '';
        });
        
        stackItems.forEach(item => {
            // Use flex basis percentage for equal distribution
            item.style.flex = `0 1 calc(${flexBasis}% - ${8 * (bestColumns - 1) / bestColumns}px)`;
            item.style.height = Math.floor(bestHeight) + 'px';
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
    initialize: function() {
        let resizeTimeout;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(() => {
                // Re-optimize after resize with a small delay
                const streamCount = document.querySelectorAll('.stream-card').length;
                if (streamCount > 0) {
                    this.optimizeStreamLayout(streamCount);
                }
            }, 100);
        });
    }
};

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.streamOptimizer.initialize();
});
