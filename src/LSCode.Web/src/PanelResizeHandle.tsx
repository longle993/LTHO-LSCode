import { useEffect, useRef } from "react";

export function PanelResizeHandle() {
  const drag = useRef<{ pointer: number; startY: number; height: number }>();
  const frame = useRef(0);
  useEffect(() => () => cancelAnimationFrame(frame.current), []);

  function resize(handle: HTMLElement, height: number) {
    const area = handle.parentElement;
    if (!area) return;
    const max = Math.max(80, Math.floor(area.clientHeight * 0.65));
    const next = Math.round(Math.min(max, Math.max(80, height)));
    area.style.setProperty("--results-height", `${next}px`);
  }

  return (
    <div
      className="results-resize-handle"
      role="separator"
      aria-orientation="horizontal"
      aria-label="Đổi chiều cao panel kết quả"
      aria-controls="results-panel"
      tabIndex={0}
      onPointerDown={(event) => {
        if (event.button !== 0 || !event.isPrimary) return;
        const handle = event.currentTarget;
        const panel = handle.nextElementSibling;
        if (!panel) return;
        event.preventDefault();
        handle.focus();
        handle.setPointerCapture(event.pointerId);
        drag.current = { pointer: event.pointerId, startY: event.clientY, height: panel.getBoundingClientRect().height };
      }}
      onPointerMove={(event) => {
        const active = drag.current;
        if (!active || event.pointerId !== active.pointer) return;
        const handle = event.currentTarget;
        const height = active.height + active.startY - event.clientY;
        cancelAnimationFrame(frame.current);
        frame.current = requestAnimationFrame(() => resize(handle, height));
      }}
      onLostPointerCapture={() => { drag.current = undefined; }}
      onKeyDown={(event) => {
        const handle = event.currentTarget;
        const height = handle.nextElementSibling?.getBoundingClientRect().height;
        if (height === undefined || !["ArrowUp", "ArrowDown", "Home", "End"].includes(event.key)) return;
        event.preventDefault();
        const step = event.shiftKey ? 64 : 16;
        resize(handle, { ArrowUp: height + step, ArrowDown: height - step, Home: 80, End: Infinity }[event.key] ?? height);
      }}
    />
  );
}
