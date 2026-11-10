import ReactDOM from "react-dom/client";
import App from "./Startup";
import ScrollbarAutohide from "./ScrollbarAutohide";
import "./styles.css";
import "./motion.css";
ReactDOM.createRoot(document.getElementById("root")!).render(
  <>
    <svg width="0" height="0" className="logo-filter" aria-hidden="true">
      {/* The supplied PNG has an opaque checkerboard. Keep the red and teal artwork. */}
      <filter id="logo-color-mask" colorInterpolationFilters="sRGB">
        <feColorMatrix in="SourceGraphic" result="red" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  6 -6 0 0 -0.12" />
        <feColorMatrix in="SourceGraphic" result="teal" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  -6 6 0 0 -0.12" />
        <feComposite in="red" in2="teal" operator="over" result="mask" />
        <feComposite in="SourceGraphic" in2="mask" operator="in" />
      </filter>
    </svg>
    <ScrollbarAutohide />
    <App />
  </>,
);
