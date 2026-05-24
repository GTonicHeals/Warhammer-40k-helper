window.mapNodeInterop = (function () {

    var editorCleanups  = new WeakMap();
    var warRoomCleanups = new WeakMap();

    var MIN_ZOOM = 0.15;
    var MAX_ZOOM = 5.0;

    function viewport(mapEl) { return mapEl.parentElement; }

    function setTransform(mapEl, tx, ty, zoom) {
        mapEl.style.transform = 'translate(' + tx + 'px,' + ty + 'px) scale(' + zoom + ')';
    }

    function zoomAt(mx, my, oldZoom, newZoom, tx, ty) {
        return {
            tx: mx - (mx - tx) / oldZoom * newZoom,
            ty: my - (my - ty) / oldZoom * newZoom
        };
    }

    // Show the entire world centred in the viewport.
    function defaultView(vpW, vpH, natW, natH) {
        var zoom = Math.min(vpW / natW, vpH / natH);
        zoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom));
        return {
            tx:   (vpW - natW * zoom) / 2,
            ty:   (vpH - natH * zoom) / 2,
            zoom: zoom
        };
    }

    // Zoom/pan so all placed nodes are visible with padding.
    // Falls back to full-world view when no nodes are present.
    function fitToNodes(mapEl) {
        var vp   = viewport(mapEl);
        var vpW  = vp.clientWidth;
        var vpH  = vp.clientHeight;
        var natW = mapEl.offsetWidth  || vpW;
        var natH = mapEl.offsetHeight || vpH;

        var nodeEls = mapEl.querySelectorAll('[data-node-id]');
        if (!nodeEls.length) return defaultView(vpW, vpH, natW, natH);

        var minX = 1, maxX = 0, minY = 1, maxY = 0, found = false;
        nodeEls.forEach(function (el) {
            var x = parseFloat(el.style.left) / 100;
            var y = parseFloat(el.style.top)  / 100;
            if (isNaN(x) || isNaN(y)) return;
            if (x < minX) minX = x;  if (x > maxX) maxX = x;
            if (y < minY) minY = y;  if (y > maxY) maxY = y;
            found = true;
        });
        if (!found) return defaultView(vpW, vpH, natW, natH);

        // Guard single-node case
        if (maxX - minX < 0.01) { minX -= 0.05; maxX += 0.05; }
        if (maxY - minY < 0.01) { minY -= 0.05; maxY += 0.05; }

        var PAD = 0.10;
        minX = Math.max(0, minX - PAD);  maxX = Math.min(1, maxX + PAD);
        minY = Math.max(0, minY - PAD);  maxY = Math.min(1, maxY + PAD);

        var bbW  = (maxX - minX) * natW;
        var bbH  = (maxY - minY) * natH;
        var zoom = Math.min(vpW / bbW, vpH / bbH);
        zoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom));

        var bbCx = (minX + maxX) / 2 * natW;
        var bbCy = (minY + maxY) / 2 * natH;
        return {
            tx:   vpW / 2 - bbCx * zoom,
            ty:   vpH / 2 - bbCy * zoom,
            zoom: zoom
        };
    }

    // ── Editor: drag nodes + pan background + zoom ──────────────────────────
    function initEditor(mapEl, dotNetRef) {
        var dragging = null;
        var panning  = null;
        var view = fitToNodes(mapEl);
        var tx = view.tx, ty = view.ty, zoom = view.zoom;

        mapEl.style.transformOrigin = '0 0';
        mapEl.style.cursor = 'grab';
        setTransform(mapEl, tx, ty, zoom);

        var vp = viewport(mapEl);

        function onWheel(e) {
            e.preventDefault();
            var factor  = e.deltaY > 0 ? 0.9 : 1.1;
            var newZoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom * factor));
            var rect    = vp.getBoundingClientRect();
            var upd     = zoomAt(e.clientX - rect.left, e.clientY - rect.top, zoom, newZoom, tx, ty);
            tx = upd.tx; ty = upd.ty; zoom = newZoom;
            setTransform(mapEl, tx, ty, zoom);
        }

        function onMouseDown(e) {
            var nodeEl = e.target.closest('[data-node-id]');
            if (nodeEl) {
                // Drag node
                e.preventDefault();
                var rect = mapEl.getBoundingClientRect();
                dragging = {
                    nodeEl:      nodeEl,
                    nodeId:      nodeEl.dataset.nodeId,
                    startMouseX: e.clientX,
                    startMouseY: e.clientY,
                    startLeft:   parseFloat(nodeEl.style.left),
                    startTop:    parseFloat(nodeEl.style.top),
                    rectW:       rect.width,
                    rectH:       rect.height
                };
                nodeEl.classList.add('dragging');
                mapEl.classList.add('map-dragging');
            } else {
                // Pan background
                e.preventDefault();
                panning = { startX: e.clientX, startY: e.clientY, startTx: tx, startTy: ty };
                mapEl.style.cursor = 'grabbing';
            }
        }

        function onMouseMove(e) {
            if (dragging) {
                var dx      = (e.clientX - dragging.startMouseX) / dragging.rectW * 100;
                var dy      = (e.clientY - dragging.startMouseY) / dragging.rectH * 100;
                var newLeft = Math.max(1, Math.min(99, dragging.startLeft + dx));
                var newTop  = Math.max(1, Math.min(99, dragging.startTop  + dy));
                dragging.nodeEl.style.left = newLeft + '%';
                dragging.nodeEl.style.top  = newTop  + '%';
            } else if (panning) {
                tx = panning.startTx + (e.clientX - panning.startX);
                ty = panning.startTy + (e.clientY - panning.startY);
                setTransform(mapEl, tx, ty, zoom);
            }
        }

        function onMouseUp() {
            if (dragging) {
                var x = parseFloat(dragging.nodeEl.style.left) / 100;
                var y = parseFloat(dragging.nodeEl.style.top)  / 100;
                dragging.nodeEl.classList.remove('dragging');
                mapEl.classList.remove('map-dragging');
                dotNetRef.invokeMethodAsync('OnNodeMoved', dragging.nodeId, x, y);
                dragging = null;
            } else if (panning) {
                panning = null;
                mapEl.style.cursor = 'grab';
            }
        }

        vp.addEventListener('wheel', onWheel, { passive: false });
        mapEl.addEventListener('mousedown', onMouseDown);
        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup',   onMouseUp);

        editorCleanups.set(mapEl, function () {
            vp.removeEventListener('wheel', onWheel);
            mapEl.removeEventListener('mousedown', onMouseDown);
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup',   onMouseUp);
        });
    }

    // ── War Room: pan + zoom, fits nodes on first load ───────────────────────
    function initWarRoom(mapEl) {
        var panning = null;
        var view = fitToNodes(mapEl);
        var tx = view.tx, ty = view.ty, zoom = view.zoom;

        mapEl.style.transformOrigin = '0 0';
        mapEl.style.cursor = 'grab';
        setTransform(mapEl, tx, ty, zoom);

        var vp = viewport(mapEl);

        function onWheel(e) {
            e.preventDefault();
            var factor  = e.deltaY > 0 ? 0.9 : 1.1;
            var newZoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom * factor));
            var rect    = vp.getBoundingClientRect();
            var upd     = zoomAt(e.clientX - rect.left, e.clientY - rect.top, zoom, newZoom, tx, ty);
            tx = upd.tx; ty = upd.ty; zoom = newZoom;
            setTransform(mapEl, tx, ty, zoom);
        }

        function onMouseDown(e) {
            if (e.target.closest('[data-node-id]')) return;
            e.preventDefault();
            panning = { startX: e.clientX, startY: e.clientY, startTx: tx, startTy: ty };
            mapEl.style.cursor = 'grabbing';
        }

        function onMouseMove(e) {
            if (!panning) return;
            tx = panning.startTx + (e.clientX - panning.startX);
            ty = panning.startTy + (e.clientY - panning.startY);
            setTransform(mapEl, tx, ty, zoom);
        }

        function onMouseUp() {
            if (!panning) return;
            panning = null;
            mapEl.style.cursor = 'grab';
        }

        vp.addEventListener('wheel', onWheel, { passive: false });
        mapEl.addEventListener('mousedown', onMouseDown);
        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup',   onMouseUp);

        warRoomCleanups.set(mapEl, function () {
            vp.removeEventListener('wheel', onWheel);
            mapEl.removeEventListener('mousedown', onMouseDown);
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup',   onMouseUp);
        });
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    function destroy(mapEl) {
        var ec = editorCleanups.get(mapEl);
        if (ec) { ec(); editorCleanups.delete(mapEl); }
        var wc = warRoomCleanups.get(mapEl);
        if (wc) { wc(); warRoomCleanups.delete(mapEl); }
    }

    // Returns the world-space centre of the current viewport as normalized (x, y) in [0,1].
    // Parses the CSS transform on mapEl to recover tx/ty/zoom set by setTransform().
    function getViewCenter(mapEl) {
        var vp   = viewport(mapEl);
        var vpW  = vp.clientWidth;
        var vpH  = vp.clientHeight;
        var natW = mapEl.offsetWidth  || vpW;
        var natH = mapEl.offsetHeight || vpH;

        // Parse 'translate(tx px, ty px) scale(zoom)' from mapEl.style.transform
        var t    = mapEl.style.transform || '';
        var tm   = t.match(/translate\(\s*([-\d.]+)px,\s*([-\d.]+)px\)\s*scale\(([-\d.]+)\)/);
        var tx   = tm ? parseFloat(tm[1]) : 0;
        var ty   = tm ? parseFloat(tm[2]) : 0;
        var zoom = tm ? parseFloat(tm[3]) : 1;

        // Viewport centre in world space
        var wx = (vpW / 2 - tx) / (natW * zoom);
        var wy = (vpH / 2 - ty) / (natH * zoom);

        return {
            x: Math.max(0.02, Math.min(0.98, wx)),
            y: Math.max(0.02, Math.min(0.98, wy))
        };
    }

    return { initEditor: initEditor, initWarRoom: initWarRoom, destroy: destroy, getViewCenter: getViewCenter };
})();
