import { useEffect, useRef } from "react";

export function WorkspaceResizeHandle({ width, onCommit }: { width: number; onCommit: (width: number) => void }) {
  const drag = useRef<{ pointer: number; start: number; width: number; next: number }>();
  const frame = useRef(0);
  useEffect(() => () => cancelAnimationFrame(frame.current), []);

  function resize(handle: HTMLDivElement, next: number) {
    const workbench = handle.closest<HTMLElement>(".workbench");
    const max = Math.min(620, (workbench?.clientWidth ?? window.innerWidth) - 400);
    const value = Math.round(Math.max(210, Math.min(max, next)));
    workbench?.style.setProperty("--workspace-width", value + "px");
    handle.setAttribute("aria-valuenow", String(value));
    return value;
  }

  return <div className="workspace-resize-handle" role="separator" aria-orientation="vertical"
    aria-label="Đổi chiều rộng Không gian dự án" aria-controls="project-sidebar"
    aria-valuemin={210} aria-valuemax={620} aria-valuenow={width} tabIndex={0}
    onPointerDown={(event) => {
      if (event.button !== 0 || !event.isPrimary) return;
      const handle = event.currentTarget;
      const startWidth = handle.parentElement?.getBoundingClientRect().width ?? width;
      event.preventDefault();
      handle.focus();
      handle.setPointerCapture(event.pointerId);
      drag.current = { pointer: event.pointerId, start: event.clientX, width: startWidth, next: startWidth };
    }}
    onPointerMove={(event) => {
      const active = drag.current;
      if (!active || active.pointer !== event.pointerId) return;
      const handle = event.currentTarget;
      active.next = active.width + event.clientX - active.start;
      cancelAnimationFrame(frame.current);
      frame.current = requestAnimationFrame(() => resize(handle, active.next));
    }}
    onLostPointerCapture={(event) => {
      const active = drag.current;
      if (!active || active.pointer !== event.pointerId) return;
      drag.current = undefined;
      cancelAnimationFrame(frame.current);
      onCommit(resize(event.currentTarget, active.next));
    }}
    onKeyDown={(event) => {
      const handle = event.currentTarget;
      const start = handle.parentElement?.getBoundingClientRect().width ?? width;
      const step = event.shiftKey ? 64 : 16;
      const next = { ArrowLeft: start - step, ArrowRight: start + step, Home: 210, End: 620 }[event.key];
      if (next === undefined) return;
      event.preventDefault();
      onCommit(resize(handle, next));
    }}
  />;
}

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
