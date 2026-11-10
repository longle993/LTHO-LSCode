import { useState, useRef, useEffect } from "react";
import { Button, Modal } from "../ui";
import { request, operation } from "../bridge";
import type { Settings, Target } from "../bridge";
import { useTask } from "./useTask";
export function ConnectionDialog({
  settings,
  onClose,
  onConnected,
}: {
  settings: Settings;
  onClose: () => void;
  onConnected: (t: Target, settings: Settings) => void;
}) {
  const [form, setForm] = useState({
    server: settings.server,
    database: settings.database,
    integratedSecurity: settings.integratedSecurity,
    user: settings.sqlUser,
    password: "",
    trustCertificate: settings.trustCertificate,
  });
  const task = useTask();
  const pending = useRef<{ cancel: () => Promise<unknown> }>(),
    alive = useRef(true);
  useEffect(
    () => () => {
      alive.current = false;
      void pending.current?.cancel().catch(() => {});
    },
    [],
  );
  return (
    <Modal title="Kết nối SQL Server" onClose={onClose}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void task.run(async () => {
            const op = operation<Target>("sql.connect", form);
            pending.current = op;
            const t = await op.promise;
            if (!alive.current) return;
            const next = {
              ...settings,
              server: form.server,
              database: form.database,
              integratedSecurity: form.integratedSecurity,
              sqlUser: form.user,
              trustCertificate: form.trustCertificate,
            };
            await request("settings.save", { settings: next });
            setForm((v) => ({ ...v, password: "" }));
            if (alive.current) onConnected(t, next);
          });
        }}
      >
        <div className="form">
          <label>
            Server
            <input
              required
              value={form.server}
              onChange={(e) => setForm({ ...form, server: e.target.value })}
            />
          </label>
          <label>
            Database
            <input
              required
              value={form.database}
              onChange={(e) => setForm({ ...form, database: e.target.value })}
            />
          </label>
          <label className="checkbox">
            <input
              type="checkbox"
              checked={form.integratedSecurity}
              onChange={(e) =>
                setForm({ ...form, integratedSecurity: e.target.checked })
              }
            />
            Windows Authentication
          </label>
          {!form.integratedSecurity && (
            <div className="form-row">
              <label>
                Tài khoản
                <input
                  required
                  autoComplete="off"
                  value={form.user}
                  onChange={(e) => setForm({ ...form, user: e.target.value })}
                />
              </label>
              <label>
                Mật khẩu
                <input
                  type="password"
                  autoComplete="off"
                  value={form.password}
                  onChange={(e) =>
                    setForm({ ...form, password: e.target.value })
                  }
                />
              </label>
            </div>
          )}
          <label className="checkbox">
            <input
              type="checkbox"
              checked={form.trustCertificate}
              onChange={(e) =>
                setForm({ ...form, trustCertificate: e.target.checked })
              }
            />
            Trust server certificate
          </label>
          <p className="muted">
            Mật khẩu chỉ dùng trong phiên kết nối, không lưu vào settings.
          </p>
          <p className="form-error" role="alert">
            {task.error}
          </p>
        </div>
        <div className="modal-footer">
          <Button type="button" onClick={onClose}>
            Hủy
          </Button>
          <Button variant="primary" disabled={task.busy}>
            {task.busy ? "Đang kết nối…" : "Kết nối"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
