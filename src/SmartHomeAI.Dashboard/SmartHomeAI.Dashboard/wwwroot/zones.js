// Trình vẽ vùng: dữ liệu đi qua proxy cùng nguồn /api/ai/*; tọa độ chuẩn hóa 0..1.
(function () {
    const NAMES = ['livingroom', 'sofa', 'door', 'other_exit'];
    const VI = { livingroom: 'Phòng khách', sofa: 'Sofa', door: 'Cửa', other_exit: 'Lối sang khu khác' };
    const COL = { livingroom: '#3b82f6', sofa: '#f59e0b', door: '#ef4444', other_exit: '#a855f7' };
    const NS = 'http://www.w3.org/2000/svg';
    let root, wrap, zones, sel, drawing, selV, msg, svg, stage, ar = 4 / 3, drag;

    const el = (t, a, ...k) => { const e = document.createElement(t); Object.assign(e, a || {}); e.append(...k); return e; };
    const sv = (t, a) => { const e = document.createElementNS(NS, t); for (const k in a) e.setAttribute(k, a[k]); return e; };
    const clamp = v => Math.min(1, Math.max(0, v));
    const pt = e => { const r = svg.getBoundingClientRect(); return [clamp((e.clientX - r.left) / r.width), clamp((e.clientY - r.top) / r.height)]; };
    const near = (a, b) => { const r = svg.getBoundingClientRect(); return Math.hypot((a[0] - b[0]) * r.width, (a[1] - b[1]) * r.height) < 20; };

    async function api(method, path, body) {
        try {
            const r = await fetch('/api/ai/' + path, { method, headers: { 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) });
            const j = await r.json().catch(() => ({}));
            return r.ok ? { ok: true, j } : { ok: false, err: j.error || ('Lỗi ' + r.status) };
        } catch (e) { return { ok: false, err: 'Không nối được máy chủ' }; }
    }

    function fit() {
        if (!stage) return;
        const w = stage.clientWidth, h = stage.clientHeight; if (!w || !h) return;
        const sw = Math.min(w, h * ar);
        wrap.style.width = sw + 'px'; wrap.style.height = sw / ar + 'px';
    }

    function loadFrame() {
        const im = svg.querySelector('image');
        const u = '/api/ai/frame.jpg?t=' + Date.now();
        fetch(u).then(r => r.ok ? r.blob() : Promise.reject()).then(b => {
            const url = URL.createObjectURL(b), t = new Image();
            t.onload = () => { ar = t.naturalWidth / t.naturalHeight; im.setAttribute('href', url); fit(); };
            t.src = url;
        }).catch(() => say('Không lấy được khung hình (AI đang chạy chưa?).', true));
    }

    function say(m, bad) { msg = { m, bad }; const n = root.querySelector('.zmsg'); if (n) { n.textContent = m; n.className = 'zmsg' + (bad ? ' err' : ''); } }

    function render() {
        // vẽ lại SVG (trừ ảnh nền) và danh sách
        [...svg.querySelectorAll('.zl')].forEach(n => n.remove()); wrap.querySelectorAll('.zlbls').forEach(n => n.remove());
        const lab = el('div', { className: 'zlbls' }); wrap.append(lab);
        const g = sv('g', { class: 'zl' }); svg.append(g);
        for (const n of Object.keys(zones).sort((a, b) => (b === 'livingroom') - (a === 'livingroom'))) {
            const c = COL[n], pts = zones[n], on = n === sel;
            const pl = sv('polygon', { points: pts.map(p => p.join(',')).join(' '), fill: c, 'fill-opacity': on ? .3 : .15, stroke: c, 'stroke-width': on ? 3 : 2, 'vector-effect': 'non-scaling-stroke' });
            pl.style.cursor = 'pointer'; pl.addEventListener('pointerdown', e => { if (!drawing) { sel = n; selV = null; e.stopPropagation(); render(); } });
            g.append(pl);
            const cx = pts.reduce((a, p) => a + p[0], 0) / pts.length, cy = pts.reduce((a, p) => a + p[1], 0) / pts.length;
            const t = el('div', { className: 'zlbl', textContent: VI[n] }); t.style.left = cx * 100 + '%'; t.style.top = cy * 100 + '%'; lab.append(t);
            if (on) pts.forEach((p, i) => {
                const d = sv('path', { d: `M${p[0]} ${p[1]}L${p[0]} ${p[1]}`, stroke: i === selV ? '#fff' : c, 'stroke-width': 18, 'stroke-linecap': 'round', 'vector-effect': 'non-scaling-stroke', fill: 'none' });
                d.style.cursor = 'grab'; d.style.touchAction = 'none';
                d.addEventListener('pointerdown', e => { if (drawing) return; e.stopPropagation(); selV = i; drag = { n, i }; svg.setPointerCapture(e.pointerId); render(); });
                d.addEventListener('dblclick', e => { e.stopPropagation(); delV(i); });
                g.append(d);
            });
        }
        if (drawing) {
            const c = COL[sel], pts = drawing;
            if (pts.length > 1) g.append(sv('polyline', { points: pts.map(p => p.join(',')).join(' '), fill: 'none', stroke: c, 'stroke-width': 3, 'vector-effect': 'non-scaling-stroke' }));
            pts.forEach((p, i) => g.append(sv('path', { d: `M${p[0]} ${p[1]}L${p[0]} ${p[1]}`, stroke: i === 0 ? '#fff' : c, 'stroke-width': 18, 'stroke-linecap': 'round', 'vector-effect': 'non-scaling-stroke' })));
        }
        const side = root.querySelector('.zside'); side.replaceChildren();
        for (const n of NAMES) {
            const b = el('button', { className: 'btn zitem' + (n === sel ? ' on' : ''), onclick: () => { if (drawing) return; sel = n; selV = null; render(); } },
                el('span', { className: 'zsw' }), VI[n] + (zones[n] ? '' : ' (chưa có)'));
            b.firstChild.style.background = COL[n]; side.append(b);
        }
        const act = (t, f, dis) => side.append(el('button', { className: 'btn', textContent: t, disabled: !!dis, onclick: f }));
        if (drawing) {
            act('Xong', finish, drawing.length < 3); act('Hủy vẽ', () => { drawing = null; render(); });
        } else {
            act('Vẽ mới', () => { drawing = []; selV = null; say('Bấm lần lượt từng đỉnh; bấm đỉnh đầu hoặc nút Xong để khép.'); render(); });
            act('Xóa đỉnh', () => delV(selV), selV === null);
            act('Xóa vùng', () => { delete zones[sel]; render(); }, sel === 'livingroom' || !zones[sel]);
            act('Khôi phục mặc định', restore);
        }
        act('Chụp lại', loadFrame);
        act('Lưu', save, !!drawing); act('Hủy', close, false);
        side.append(el('div', { className: 'zmsg' + (msg?.bad ? ' err' : ''), textContent: msg?.m || '' }));
    }

    function finish() {
        if (drawing.length < 3) return;
        zones[sel] = drawing; drawing = null; say('Đã khép vùng ' + VI[sel] + '. Nhớ bấm Lưu.'); render();
    }
    function delV(i) {
        if (i === null || !zones[sel]) return;
        if (zones[sel].length <= 3) return say('Vùng cần ít nhất 3 đỉnh.', true);
        zones[sel].splice(i, 1); selV = null; render();
    }
    async function restore() {
        const r = await api('GET', 'zones/default'); if (!r.ok) return say(r.err, true);
        zones = r.j.zones; if (!zones[sel]) sel = 'livingroom'; say('Đã nạp vùng mặc định (chưa lưu).'); render();
    }
    async function save() {
        const r = await api('PUT', 'zones', { zones });
        if (!r.ok) return say('AI từ chối: ' + r.err, true);
        say('Đã lưu và áp dụng.'); const g = await api('GET', 'zones'); if (g.ok) { zones = g.j.zones; render(); }
    }
    function close() { root.hidden = true; root.replaceChildren(); drag = null; drawing = null; }

    window.zoneEditor = {
        async open() {
            root = document.getElementById('zed'); if (!root) return;
            const r = await api('GET', 'zones');
            if (!r.ok) { alert('Không đọc được vùng từ AI: ' + r.err); return; }
            zones = r.j.zones; sel = 'livingroom'; selV = null; drawing = null; msg = null; drag = null;
            stage = el('div', { className: 'zstage' });
            svg = sv('svg', { viewBox: '0 0 1 1', preserveAspectRatio: 'none' }); svg.style.touchAction = 'none';
            svg.append(sv('image', { x: 0, y: 0, width: 1, height: 1, preserveAspectRatio: 'none' }));
            svg.addEventListener('pointerdown', e => {
                if (!drawing) return; const p = pt(e);
                if (drawing.length >= 3 && near(p, drawing[0])) return finish();
                if (drawing.length < 20) { drawing.push(p); render(); }
            });
            svg.addEventListener('pointermove', e => { if (drag) { zones[drag.n][drag.i] = pt(e); render(); } });
            const up = () => { drag = null; }; svg.addEventListener('pointerup', up); svg.addEventListener('pointercancel', up);
            wrap = el('div', { className: 'zwrap' }, svg); stage.append(wrap);
            root.replaceChildren(stage, el('div', { className: 'zside' }));
            root.hidden = false;
            new ResizeObserver(fit).observe(stage);
            loadFrame(); render(); fit();
        }
    };
})();
