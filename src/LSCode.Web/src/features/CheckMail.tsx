import { useEffect, useRef, useState, type FormEvent } from "react";
import { operation } from "../bridge";
import { Button, Select } from "../ui";
import "./checkMail.css";

type Result = { success: boolean; message: string; field?: string };
const securityOptions = [
  { value: "starttls", label: "STARTTLS" },
  { value: "tls", label: "TLS ngay khi kết nối" },
  { value: "none", label: "Kết nối thường" },
];

export default function CheckMail() {
  const [security, setSecurity] = useState("starttls");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Result>();
  const pending = useRef<ReturnType<typeof operation<Result>>>();
  useEffect(() => () => {
    const request = pending.current;
    pending.current = undefined;
    void request?.cancel().catch(() => {});
  }, []);

  async function send(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending.current) return;
    const form = event.currentTarget;
    const values = Object.fromEntries(new FormData(form));
    setBusy(true);
    setResult(undefined);
    const request = operation<Result>("mail.sendTest", {
      ...values,
      port: Number(values.port),
      security,
      html: values.format === "html",
    });
    pending.current = request;
    try {
      const response = await request.promise;
      if (pending.current !== request) return;
      setResult(response);
      if (response.field) {
        requestAnimationFrame(() => {
          const field = form.elements.namedItem(response.field!);
          if (field instanceof HTMLElement) field.focus();
        });
      }
    } catch {
      if (pending.current === request)
        setResult({ success: false, message: "Không nhận được kết quả từ LSCode." });
    } finally {
      if (pending.current === request) {
        pending.current = undefined;
        setBusy(false);
      }
    }
  }

  function field(name: string, label: string, options: React.InputHTMLAttributes<HTMLInputElement> = {}) {
    return <label className="check-mail-field">
      <span>{label}</span>
      <input name={name} aria-invalid={result?.field === name}
        aria-describedby={result?.field === name ? "check-mail-result" : undefined} {...options} />
    </label>;
  }

  function recipients(name: string, label: string, required = false) {
    return <label className="check-mail-field">
      <span>{label}</span>
      <textarea name={name} rows={2} required={required}
        placeholder={required ? "Ngăn cách bằng dấu ; hoặc xuống dòng" : undefined}
        aria-invalid={result?.field === name}
        aria-describedby={result?.field === name ? "check-mail-result" : undefined} />
    </label>;
  }
  return <div className="check-mail">
    <form onSubmit={send} autoComplete="off">
      <fieldset className="check-mail-form" disabled={busy}>
        <div className="check-mail-connection">
          {field("host", "Máy chủ SMTP", { required: true, placeholder: "smtp.example.test" })}
          {field("port", "Cổng", { required: true, type: "number", min: 1, max: 65535, defaultValue: 587 })}
          <div className="check-mail-field">
            <span>Bảo mật</span>
            <Select label="Bảo mật" value={security} options={securityOptions} onChange={setSecurity} disabled={busy} floating />
            {result?.field === "security" && <span className="check-mail-error">{result.message}</span>}
          </div>
        </div>
        <div className="check-mail-addresses">
          <fieldset className="check-mail-group">
            <legend>Người gửi</legend>
            {field("from", "Email", { required: true, type: "email" })}
            {field("alias", "Tên hiển thị")}
            {field("account", "Tài khoản")}
            {field("password", "Mật khẩu", { type: showPassword ? "text" : "password", autoComplete: "new-password", spellCheck: false })}
            <label className="check-mail-choice"><input type="checkbox" checked={showPassword} onChange={e => setShowPassword(e.target.checked)} /> Hiện mật khẩu</label>
          </fieldset>
          <fieldset className="check-mail-group">
            <legend>Người nhận</legend>
            {recipients("to", "To", true)}
            {recipients("cc", "Cc")}
            {recipients("bcc", "Bcc")}
          </fieldset>
        </div>
        {field("subject", "Tiêu đề")}
        <div className="check-mail-field">
          <label htmlFor="check-mail-body">Nội dung</label>
          <div className="check-mail-formats" role="group" aria-label="Định dạng nội dung">
            <label className="check-mail-choice"><input type="radio" name="format" value="text" defaultChecked /> Text</label>
            <label className="check-mail-choice"><input type="radio" name="format" value="html" /> HTML</label>
          </div>
          <textarea id="check-mail-body" name="body" rows={5} />
        </div>
      </fieldset>
      <div className="check-mail-result">
        <strong>Kết quả</strong>
        <div id="check-mail-result" role="status" className={result && !result.success ? "check-mail-error" : undefined}>
          {busy ? "Đang gửi thử…" : result?.message ?? "Chưa gửi thư thử."}
        </div>
      </div>
      <Button type="submit" variant="primary" disabled={busy}>{busy ? "Đang gửi…" : "Gửi thử"}</Button>
    </form>
  </div>;
}
