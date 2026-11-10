import ReactDOM from "react-dom/client";
import App from "./Startup";
import ScrollbarAutohide from "./ScrollbarAutohide";
import "./styles.css";
ReactDOM.createRoot(document.getElementById("root")!).render(
  <>
    <ScrollbarAutohide />
    <App />
  </>,
);
