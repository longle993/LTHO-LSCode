import { useState } from "react";
export function useTask() {
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  async function run<T>(fn: () => Promise<T>) {
    setBusy(true);
    setError("");
    try {
      return await fn();
    } catch (e) {
      setError(
        (e as Error & { code?: string }).code === "cancelled"
          ? ""
          : (e as Error).message,
      );
      return undefined;
    } finally {
      setBusy(false);
    }
  }
  return { busy, error, clearError: () => setError(""), run };
}
