// Display author information in console
(function() {
    const styles = {
        title: 'font-size: 18px; font-weight: bold; color: #a78bfa; padding: 4px 0;',
        label: 'font-size: 13px; color: #9ca3af;',
        value: 'font-size: 13px; color: #e5e7eb; font-weight: 500;',
        link: 'font-size: 13px; color: #60a5fa; font-weight: 500;',
        divider: 'color: #4b5563;'
    };

    console.log('%c━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━', styles.divider);
    console.log('%c🎬 Multi-Stream Viewer', styles.title);
    console.log('%c━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━', styles.divider);
    console.log('%cAuthor:%c Patrick Magee', styles.label, styles.value);
    console.log('%cWebsite:%c https://magaoidh.pro', styles.label, styles.link);
    console.log('%c━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━', styles.divider);
    console.log('');
})();
