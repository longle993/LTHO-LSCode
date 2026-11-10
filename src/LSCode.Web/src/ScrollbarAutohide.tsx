import { useEffect } from "react";
export default function ScrollbarAutohide() {
  useEffect(() => {
    const timers = new Map<Element, number>();
    const scroll = (event: Event) => {
      const element =
        event.target instanceof Element
          ? event.target
          : document.scrollingElement;
      if (!element) return;
      element.classList.add("is-scrolling");
      window.clearTimeout(timers.get(element));
      timers.set(
        element,
        window.setTimeout(() => {
          element.classList.remove("is-scrolling");
          timers.delete(element);
        }, 700),
      );
    };
    window.addEventListener("scroll", scroll, { capture: true, passive: true });
    return () => {
      window.removeEventListener("scroll", scroll, { capture: true });
      for (const timer of timers.values()) window.clearTimeout(timer);
    };
  }, []);
  return null;
}
