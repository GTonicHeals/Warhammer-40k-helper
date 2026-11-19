// Keyboard shortcuts for GM Table Mode: "/" or Ctrl+K to search, "N" for a quick note, Esc to back out.
window.gmTableInterop = (function () {
    var handler = null;

    function isTyping(el) {
        return !!el && (/^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName) || el.isContentEditable);
    }

    function init(dotNetRef) {
        dispose();
        handler = function (e) {
            var typing = isTyping(e.target);
            if ((e.key === 'k' && (e.ctrlKey || e.metaKey)) || (e.key === '/' && !typing)) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnHotkey', 'search');
            } else if ((e.key === 'n' || e.key === 'N') && !typing && !e.ctrlKey && !e.metaKey && !e.altKey) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnHotkey', 'capture');
            } else if (e.key === 'Escape') {
                dotNetRef.invokeMethodAsync('OnHotkey', 'escape');
            }
        };
        document.addEventListener('keydown', handler);
    }

    function dispose() {
        if (handler) document.removeEventListener('keydown', handler);
        handler = null;
    }

    return { init: init, dispose: dispose };
})();
