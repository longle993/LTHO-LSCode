import { useEffect, useRef, useState } from "react";

export function LoadingLine({ busy, label }: { busy: boolean; label: string }) {
  const [visible, setVisible] = useState(false);
  const shownAt = useRef(0);

  useEffect(() => {
    if (busy === visible) return;
    const delay = busy ? 300 : Math.max(0, 500 - (performance.now() - shownAt.current));
    const timer = window.setTimeout(() => {
      if (busy) shownAt.current = performance.now();
      setVisible(busy);
    }, delay);
    return () => window.clearTimeout(timer);
  }, [busy, visible]);

  return visible ? <span className="loading-line before:animate-studio-sweep motion-reduce:before:animate-none" role="status" aria-label={busy ? label : undefined} aria-hidden={!busy} /> : null;
}
