import ControllerExplorer, { FileIcon } from "./ControllerExplorer";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  SunMoon,
  Minus,
  Maximize2,
  Files,
  Search,
  Database,
  FolderOpen,
  FilePlus2,
  Save,
  Play,
  Square,
  Settings2,
  Code2,
  ChevronRight,
  ChevronDown,
  FileCode2,
  Folder,
  Columns2,
  Braces,
  SlidersHorizontal,
  PanelBottom,
  X,
  Command,
  RefreshCw,
  Activity,
  GitCompareArrows,
  Blocks,
  Languages,
  ArrowUpRight,
} from "lucide-react";
import { native, request, operation, subscribe, quoteSqlName } from "./bridge";
import type {
  Bootstrap,
  DocumentInfo,
  Target,
  Table,
  QueryResult,
  Project,
  Settings,
  Snippet,
} from "./bridge";
import { commands as commandList, migratePreferences } from "./commands";
import {
  CodeEditor,
  modelFor,
  textOf,
  disposeModel,
  language,
  markSqlErrors,
} from "./editor";
import type { EditorHandle } from "./editor";
import { Button, IconButton, Select, Modal, DataGrid } from "./ui";
import {
  ToolDialog,
  ProjectDialog,
  ConnectionDialog,
  SettingsDialog,
} from "./tools";

type Tab = DocumentInfo & { saved: string; target?: Target; project?: string };
type Entry = { path: string; name: string; directory: boolean };
type Hit = { path: string; line: number; column: number; preview: string };
const initialSettings: Settings = {
  theme: "Light",
  workspace: null,
  server: ".",
  database: "master",
  integratedSecurity: true,
  sqlUser: "",
  trustCertificate: false,
  crystalPath: "",
  profiler18Path: "",
  shortcuts: {},
};

export default function App({
  initialProjectId,
  bootstrap,
}: { initialProjectId?: string; bootstrap?: Bootstrap } = {}) {
  const [diagnostics, setDiagnostics] = useState<
    { line: number; column: number; message: string }[]
  >([]);
  const [settings, setSettings] = useState<Settings>(initialSettings),
    [projects, setProjects] = useState<Project[]>([]),
    [snippets, setSnippets] = useState<Snippet[]>([]);
  const [paneIds, setPaneIds] = useState(["", ""]),
    [focusedPane, setFocusedPane] = useState(0);
  const paneRef = useRef(0);
  paneRef.current = focusedPane;
  const [tabs, setTabs] = useState<Tab[]>([]),
    [activeId, setActiveIdRaw] = useState(""),
    [workspace, setWorkspace] = useState(""),
    [project, setProject] = useState<Project>();
  const setActiveId = (id: string) => {
    setActiveIdRaw(id);
    setPaneIds((old) => old.map((v, i) => (i === paneRef.current ? id : v)));
  };
  const [targets, setTargets] = useState<Target[]>([]),
    [nav, setNav] = useState("files"),
    [side, setSide] = useState(window.innerWidth > 780),
    [split, setSplit] = useState(false),
    [panel, setPanel] = useState(true);
  const [dialog, setDialog] = useState(""),
    [palette, setPalette] = useState(""),
    [ready, setReady] = useState(!native),
    [status, setStatus] = useState("Sẵn sàng"),
    [error, setError] = useState(""),
    [cursor, setCursor] = useState("Dòng 1, cột 1");
  const [result, setResult] = useState<QueryResult>(),
    [resultIndex, setResultIndex] = useState(0),
    [bottom, setBottom] = useState("results"),
    [running, setRunning] = useState(false);
  const [hits, setHits] = useState<Hit[]>([]),
    [search, setSearch] = useState(""),
    [regex, setRegex] = useState(false),
    [matchCase, setMatchCase] = useState(false),
    [searching, setSearching] = useState(false);
  const [objects, setObjects] = useState<Table>(),
    [objectQuery, setObjectQuery] = useState(""),
    [objectOffset, setObjectOffset] = useState(0),
    [contentSearch, setContentSearch] = useState(false),
    [dbBusy, setDbBusy] = useState(false);
  const [commands, setCommands] = useState<CommandNode[]>([]),
    [menuQuery, setMenuQuery] = useState(""),
    [menuIndex, setMenuIndex] = useState(-1),
    [controllerFiles, setControllerFiles] = useState<
      { name: string; files: string[] }[]
    >([]);
  const [unsavedPrompt, setUnsavedPrompt] = useState<{
    names: string[];
    resolve: (choice: "save" | "discard" | "cancel") => void;
  }>();
  const askUnsaved = (names: string[]) =>
    new Promise<"save" | "discard" | "cancel">((resolve) =>
      setUnsavedPrompt({ names, resolve }),
    );
  const [peek, setPeek] = useState<{
    document: DocumentInfo;
    line: number;
    column: number;
  }>();
  const resultsByTab = useRef(new Map<string, QueryResult>());
  const [external, setExternal] = useState<Tab>(),
    [refresh, setRefresh] = useState(0);
  const primaryEditor = useRef<EditorHandle>();
  const editor = useRef<EditorHandle>(),
    latest = useRef({ tabs, activeId, settings }),
    runRef = useRef<ReturnType<typeof operation<QueryResult>>>(),
    searchRef = useRef<ReturnType<typeof operation<Hit[]>>>(),
    menuLast = useRef("");
  const toolDirty = useRef(false),
    untitledCounter = useRef(0);
  useEffect(() => {
    if (split) setPaneIds((old) => old.map((id) => id || activeId));
    if (!split) {
      editor.current = primaryEditor.current;
      paneRef.current = 0;
      setFocusedPane(0);
      if (paneIds[0] && tabs.some((t) => t.id === paneIds[0]))
        setActiveIdRaw(paneIds[0]);
    }
  }, [split]);
  const suppress = useRef(false),
    notified = useRef(new Set<string>()),
    dbGeneration = useRef(0),
    dbLoadedTarget = useRef(""),
    defaultTarget = useRef<Target>(),
    projectTargetIds = useRef<string[]>([]),
    pendingOpens = useRef(new Map<string, Promise<void>>());
  latest.current = { tabs, activeId, settings };
  const active = tabs.find((t) => t.id === activeId),
    target = active?.target ?? defaultTarget.current;
  const [selectedCommand, setSelectedCommand] = useState("");
  useEffect(() => {
    dbGeneration.current++;
    setObjects(undefined);
    setDbBusy(false);
    dbLoadedTarget.current = "";
    setObjectOffset(0);
  }, [target?.id]);
  useEffect(() => {
    if (
      side &&
      nav === "database" &&
      target &&
      dbLoadedTarget.current !== target.id
    )
      void guard(() => loadObjects());
  }, [side, nav, target?.id]);
  useEffect(() => {
    void searchRef.current?.cancel().catch(() => {});
    searchRef.current = undefined;
    setHits([]);
    setSearching(false);
  }, [workspace]);
  useEffect(() => {
    document
      .querySelector('.sidebar [aria-current="page"]')
      ?.scrollIntoView({ block: "nearest" });
  }, [menuIndex]);
  const dirty = (t: Tab) => textOf(t) !== t.saved;
  const guard = useCallback(
    async <T,>(work: () => Promise<T>): Promise<T | undefined> => {
      setError("");
      try {
        return await work();
      } catch (e) {
        setError((e as Error).message);
        setStatus("Thao tác chưa hoàn thành");
        return undefined;
      }
    },
    [],
  );
  const addDocument = useCallback((d: DocumentInfo, t?: Target) => {
    modelFor(d);
    setTabs((old) => [...old, { ...d, saved: d.text, target: t }]);
    setActiveId(d.id);
    return d;
  }, []);
  const newDocument = useCallback(
    (text = "", name?: string) =>
      addDocument(
        {
          id: crypto.randomUUID(),
          path: null,
          name: name ?? `Untitled-${++untitledCounter.current}.sql`,
          text,
          encoding: "utf-8",
          bom: false,
          newline: "CRLF",
          version: 1,
        },
        defaultTarget.current ??
          latest.current.tabs.find((t) => t.id === latest.current.activeId)
            ?.target ??
          targets[0],
      ),
    [addDocument, targets],
  );
  const open = useCallback(
    async (path?: string, line?: number, column?: number) => {
      if (!path) {
        for (const d of await request<DocumentInfo[]>("document.openMany")) {
          const existing = latest.current.tabs.find(
            (t) => t.path?.toLowerCase() === d.path?.toLowerCase(),
          );
          if (existing) {
            setActiveId(existing.id);
            await request("document.close", { id: d.id });
          } else addDocument(d, defaultTarget.current ?? targets[0]);
        }
        return;
      }
      const key = path.toLowerCase();
      if (pendingOpens.current.has(key)) {
        await pendingOpens.current.get(key);
        return;
      }
      const existing = latest.current.tabs.find(
        (t) => t.path?.toLowerCase() === key,
      );
      if (existing) setActiveId(existing.id);
      else {
        const work = (async () => {
          const d = await request<DocumentInfo | null>("document.open", {
            path,
          });
          if (d) addDocument(d, defaultTarget.current ?? targets[0]);
        })();
        pendingOpens.current.set(key, work);
        try {
          await work;
        } finally {
          pendingOpens.current.delete(key);
        }
      }
      if (line)
        setTimeout(() => {
          editor.current?.setPosition({
            lineNumber: line,
            column: column ?? 1,
          });
          editor.current?.revealLineInCenter(line);
        }, 50);
    },
    [addDocument, targets],
  );
  const save = useCallback(async (tab?: Tab, saveAs = false) => {
    const current =
      tab ?? latest.current.tabs.find((t) => t.id === latest.current.activeId);
    if (!current) return true;
    const text = textOf(current),
      version = current.version;
    let saved: DocumentInfo | null;
    try {
      saved = await request<DocumentInfo | null>("document.save", {
        id: current.id,
        name: current.name,
        path: current.path,
        text,
        version,
        saveAs,
      });
    } catch (e) {
      if (
        (e as Error & { code?: string }).code !== "file_readonly" ||
        !window.confirm(
          "File chỉ đọc. Cho phép ghi rồi khôi phục thuộc tính chỉ đọc?",
        )
      )
        throw e;
      saved = await request<DocumentInfo | null>("document.save", {
        id: current.id,
        name: current.name,
        path: current.path,
        text,
        version,
        saveAs,
        overwriteReadOnly: true,
      });
    }
    if (!saved) return false;
    setTabs((old) =>
      old.map((t) =>
        t.id === saved.id
          ? {
              ...t,
              ...saved,
              text: textOf(t),
              version: Math.max(t.version, saved.version),
              saved: text,
            }
          : t,
      ),
    );
    notified.current.delete(current.id);
    setStatus("Đã lưu " + saved.name);
    return true;
  }, []);
  async function closeTab(t: Tab) {
    if (dirty(t)) {
      const choice = await askUnsaved([t.name]);
      if (choice === "cancel") return;
      if (choice === "save" && !(await save(t))) return;
    }
    const remaining = latest.current.tabs.filter((x) => x.id !== t.id);
    setTabs(remaining);
    if (activeId === t.id) setActiveId(remaining.at(-1)?.id ?? "");
    disposeModel(t);
    if (native) await request("document.close", { id: t.id });
  }
  const persistRecovery = useCallback(
    () =>
      request("recovery.save", {
        documents: latest.current.tabs.filter(dirty).map((t) => ({
          name: t.name,
          text: textOf(t),
          encoding: t.encoding,
          bom: t.bom,
          newline: t.newline,
        })),
      }),
    [],
  );
  const closeWindow = useCallback(async () => {
    if (
      toolDirty.current &&
      !window.confirm(
        "Danh mục còn bản nháp chưa lưu. Đóng ứng dụng sẽ bỏ bản nháp này. Tiếp tục?",
      )
    )
      return;
    const dirtyTabs = latest.current.tabs.filter(dirty);
    if (dirtyTabs.length) {
      const choice = await askUnsaved(dirtyTabs.map((t) => t.name));
      if (choice === "cancel") return;
      if (choice === "save") {
        for (const tab of dirtyTabs) if (!(await save(tab))) return;
        await new Promise(requestAnimationFrame);
      }
    }
    try {
      await persistRecovery();
    } catch (e) {
      if (
        latest.current.tabs.some(dirty) &&
        !window.confirm(
          "Không tạo được bản khôi phục: " +
            (e as Error).message +
            " Đóng và bỏ nội dung chưa lưu?",
        )
      ) {
        setError((e as Error).message);
        return;
      }
    }
    await request("window.close");
  }, [persistRecovery]);
  useEffect(
    () =>
      subscribe((e) => {
        if (e.type === "window.closing") void guard(closeWindow);
      }),
    [closeWindow, guard],
  );
  useEffect(() => {
    if (!native) return;
    void guard(async () => {
      const b = bootstrap ?? (await request<Bootstrap>("studio.bootstrap"));
      setSettings(migratePreferences(b.settings));
      setProjects(b.projects);
      setSnippets(b.snippets);
      setWorkspace(b.settings.workspace ?? "");
      const recovery = await request<
        {
          name: string;
          text: string;
          encoding: string;
          bom: boolean;
          newline: string;
        }[]
      >("recovery.load");
      for (const r of recovery) {
        const d = await request<DocumentInfo>("document.restore", {
          ...r,
          id: crypto.randomUUID(),
          path: null,
          version: 1,
        });
        modelFor(d);
        setTabs((old) => [...old, { ...d, saved: "" }]);
        setActiveId(d.id);
      }
      if (initialProjectId) {
        const selected = b.projects.find((p) => p.id === initialProjectId);
        if (selected) await openProject(selected);
      }
      setReady(true);
      setStatus(
        recovery.length
          ? `Đã khôi phục ${recovery.length} file chưa lưu`
          : "Đã kết nối .NET",
      );
    });
  }, [guard]);
  useEffect(() => {
    document.documentElement.dataset.theme = settings.theme;
  }, [settings.theme]);
  useEffect(() => {
    setResult(resultsByTab.current.get(activeId));
    setResultIndex(0);
  }, [activeId]);
  useEffect(() => {
    if (!native || !ready) return;
    const timer = setInterval(() => void guard(persistRecovery), 8000);
    return () => clearInterval(timer);
  }, [ready, guard, persistRecovery]);
  useEffect(() => {
    if (!native || !ready) return;
    let busy = false;
    const timer = setInterval(async () => {
      if (busy) return;
      busy = true;
      try {
        for (const t of latest.current.tabs.filter(
          (x) => x.path && !notified.current.has(x.id),
        )) {
          if (await request<boolean>("document.changed", { id: t.id })) {
            notified.current.add(t.id);
            setExternal(t);
            break;
          }
        }
      } catch (e) {
        setError((e as Error).message);
      } finally {
        busy = false;
      }
    }, 2000);
    return () => clearInterval(timer);
  }, [ready]);
  useEffect(() => {
    if (!ready || !window.__LSCODE_SMOKE__) return;
    window.lscodeSmoke = async () => {
      try {
        if (workspace) {
          const paths = await request<string[]>("workspace.files", {
            path: workspace,
          });
          const file = paths.find((p) => p.endsWith("fixture.xml"));
          if (!file) throw new Error("Missing smoke fixture");
          const opened = await request<DocumentInfo>("document.open", {
            path: file,
          });
          await request("document.save", {
            id: opened.id,
            path: file,
            text: "<root>Đã lưu</root>\r\n",
            version: 2,
          });
          const check = await request<DocumentInfo>("document.open", {
            path: file,
          });
          if (!check.text.includes("Đã lưu") || !check.bom)
            throw new Error("Native file save/BOM");
          addDocument(check);
          setSplit(true);
        }
        const formatted = await request<string>("text.format", {
          text: "select 1 as value",
          language: "sql",
        });
        if (!formatted.includes("SELECT")) throw new Error("SQL format");
        const xml = await request<{ diagnostic: unknown }>("xml.analyze", {
          text: "<root><a/></root>",
        });
        if (xml.diagnostic) throw new Error("XML analysis");
        const data = await request<Snippet[]>("snippet.list");
        if (data.length === 0) throw new Error("Snippet assets missing");
        newDocument(
          "-- Kiểm tra WebView2\r\nSELECT 1 AS [Giá trị];",
          "webview-smoke.sql",
        );
        document.documentElement.dataset.smoke = JSON.stringify({
          passed: true,
          checks: [
            "React bootstrap",
            "native bridge",
            "SQL formatter",
            "XML parser",
            "snippet assets",
            "Monaco editor",
            "native file open/save/BOM",
            "shared split model",
          ],
        });
      } catch (e) {
        document.documentElement.dataset.smoke = JSON.stringify({
          passed: false,
          error: (e as Error).message,
        });
      }
    };
    return () => {
      delete window.lscodeSmoke;
    };
  }, [ready, newDocument, workspace, addDocument]);
  async function runSql() {
    if (!active) return;
    if (!active.target) {
      setDialog("connection");
      return;
    }
    const selection = editor.current?.getSelection();
    const selected = selection
      ? editor.current?.getModel()?.getValueInRange(selection)
      : "";
    setRunning(true);
    setPanel(true);
    setBottom("results");
    setStatus("Đang chạy SQL…");
    setError("");
    markSqlErrors(active, []);
    const queryDocument = active;
    const op = operation<QueryResult>("sql.execute", {
      targetId: active.target.id,
      sql: selected || textOf(active),
    });
    runRef.current = op;
    try {
      const r = await op.promise;
      resultsByTab.current.set(queryDocument.id, r);
      if (latest.current.activeId === queryDocument.id) {
        setResult(r);
        setResultIndex(0);
      }
      setStatus(
        `${r.durationMs.toFixed(0)} ms · ${r.tables.reduce((sum, t) => sum + t.rows.length, 0)} dòng${r.truncated ? " · Đã giới hạn kết quả" : ""}`,
      );
    } catch (e) {
      const failure = e as Error & {
        code?: string;
        details?: { line: number; message: string; procedure: string }[];
      };
      if (failure.code === "cancelled") setStatus("Đã dừng query");
      else {
        setError(failure.message);
        setBottom("messages");
        if (failure.details)
          markSqlErrors(
            queryDocument,
            failure.details,
            selected ? selection?.startLineNumber : 1,
            selected ? selection?.startColumn : 1,
          );
      }
    } finally {
      setRunning(false);
      runRef.current = undefined;
    }
  }
  async function format() {
    if (!active) return;
    const formatted = await request<string>("text.format", {
      text: textOf(active),
      language: language(active.name),
    });
    const model = modelFor(active);
    editor.current?.executeEdits("format", [
      {
        range: model.getFullModelRange(),
        text: formatted,
        forceMoveMarkers: true,
      },
    ]);
  }
  async function chooseFolder() {
    const path = await request<string | null>("workspace.folder");
    if (path) {
      setWorkspace(path);
      setControllerFiles([]);
      setNav("files");
      const next = { ...settings, workspace: path };
      setSettings(next);
      await request("settings.save", { settings: next });
    }
  }
  const execute = async (id: string) => {
    switch (id) {
      case "file.new":
        newDocument();
        break;
      case "file.open":
        await open();
        break;
      case "folder.open":
        await chooseFolder();
        break;
      case "file.close":
        if (active) await closeTab(active);
        break;
      case "window.new":
        await request("window.new");
        break;
      case "editor.find":
        editor.current?.trigger("keyboard", "actions.find", null);
        break;
      case "editor.replace":
        editor.current?.trigger(
          "keyboard",
          "editor.action.startFindReplaceAction",
          null,
        );
        break;
      case "sql.complete":
        editor.current?.trigger(
          "keyboard",
          "editor.action.triggerSuggest",
          null,
        );
        break;
      case "sql.refresh":
        await request("sql.refresh");
        setStatus("Đã xóa cache gợi ý SQL");
        break;
      case "workspace.refresh":
        setRefresh((v) => v + 1);
        break;
      case "workspace.files":
        setNav("files");
        setSide(true);
        break;
      case "workspace.objects":
        setNav("database");
        setSide(true);
        break;
      case "sql.export":
        exportCsv();
        break;
      case "db.toggle": {
        const scoped = targets.filter((t) =>
          projectTargetIds.current.includes(t.id),
        );
        if (
          !active ||
          scoped.length < 2 ||
          !scoped.some((t) => t.id === target?.id)
        ) {
          setStatus("Dự án cần kết nối App và SYS.");
          return;
        }
        const next =
          scoped[
            (scoped.findIndex((t) => t.id === target?.id) + 1) % scoped.length
          ];
        setTabs((old) =>
          old.map((t) => (t.id === activeId ? { ...t, target: next } : t)),
        );
        return;
      }
      case "theme.switch": {
        const themes = ["Light", "Dark", "High Contrast"];
        const next = {
          ...settings,
          theme: themes[(themes.indexOf(settings.theme) + 1) % 3],
        };
        await request("settings.save", { settings: next });
        setSettings(next);
        break;
      }
      case "project.metadata":
        if (project)
          newDocument(
            await request<string>("project.metadata", { id: project.id }),
            "project-metadata.xml",
          );
        else setDialog("projects");
        break;
      case "external.crystal": {
        const path = await request<string | null>("file.pick");
        if (path) await request("external.crystal", { path });
        break;
      }
      case "xml.include":
      case "xml.include.open": {
        if (!active?.path)
          throw new Error("Lưu file trước khi điều hướng include.");
        const model = modelFor(active);
        const found = await request<{
          path: string;
          line: number;
          column: number;
        } | null>("xml.include", {
          path: active.path,
          text: textOf(active),
          offset: model.getOffsetAt(
            editor.current?.getPosition() ?? { lineNumber: 1, column: 1 },
          ),
          buffers: Object.fromEntries(
            tabs.filter((t) => t.path).map((t) => [t.path!, textOf(t)]),
          ),
        });
        if (!found)
          throw new Error("Không tìm thấy include tại vị trí con trỏ.");
        if (id === "xml.include") {
          const d = await request<DocumentInfo>("document.open", {
            path: found.path,
          });
          const buffer = tabs.find(
            (t) => t.path?.toLowerCase() === found.path.toLowerCase(),
          );
          if (buffer) d.text = textOf(buffer);
          setPeek({ document: d, line: found.line, column: found.column });
        } else await open(found.path, found.line, found.column);
        break;
      }
      case "file.save":
        await save();
        break;
      case "file.saveAs":
        await save(undefined, true);
        break;
      case "sql.run":
        await runSql();
        break;
      case "sql.cancel":
        await runRef.current?.cancel();
        break;
      case "editor.split":
        setSplit((v) => !v);
        break;
      case "editor.format":
        await format();
        break;
      case "workspace.search":
        setSide(true);
        setNav("search");
        break;
      case "project.open":
        setDialog("projects");
        break;
      default:
        setDialog(id);
    }
  };
  const executeRef = useRef(execute);
  executeRef.current = execute;
  useEffect(() => {
    const keys = (e: KeyboardEvent) => {
      if (e.isComposing) return;
      const key = [
        e.ctrlKey ? "Ctrl" : "",
        e.shiftKey ? "Shift" : "",
        e.altKey ? "Alt" : "",
        e.key === " "
          ? "Space"
          : e.key.length === 1
            ? e.key.toUpperCase()
            : e.key,
      ]
        .filter(Boolean)
        .join("+");
      if (key === "Ctrl+Shift+P") {
        e.preventDefault();
        setPalette("commands");
        return;
      }
      if (key === "Ctrl+P") {
        e.preventDefault();
        setPalette("files");
        return;
      }
      const c = commandList.find(([id, , shortcut]) =>
        (latest.current.settings.shortcuts[id] ?? [shortcut]).some(
          (v) => v.toUpperCase() === key.toUpperCase(),
        ),
      );
      if (c && !document.querySelector("dialog[open]")) {
        e.preventDefault();
        e.stopImmediatePropagation();
        void guard(() => executeRef.current(c[0]));
      }
    };
    window.addEventListener("keydown", keys, true);
    return () => window.removeEventListener("keydown", keys, true);
  }, [guard]);
  async function find() {
    if (!workspace) return;
    await searchRef.current?.cancel();
    setSearching(true);
    const op = operation<Hit[]>("workspace.search", {
      path: workspace,
      query: search,
      regex,
      matchCase,
      buffers: Object.fromEntries(
        tabs.filter((t) => t.path).map((t) => [t.path!, textOf(t)]),
      ),
    });
    searchRef.current = op;
    try {
      const found = await op.promise;
      if (searchRef.current === op) setHits(found);
    } catch (e) {
      if (searchRef.current === op) setError((e as Error).message);
    } finally {
      if (searchRef.current === op) setSearching(false);
    }
  }
  async function loadObjects(offset = 0) {
    if (!target) {
      setDialog("connection");
      return;
    }
    const generation = ++dbGeneration.current;
    setDbBusy(true);
    try {
      const table = await request<Table>("schema.objects", {
        targetId: target.id,
        query: objectQuery,
        content: contentSearch,
        offset,
      });
      if (generation === dbGeneration.current) {
        dbLoadedTarget.current = target.id;
        setObjects(table);
        setObjectOffset(offset);
      }
    } catch (e) {
      if (generation === dbGeneration.current) throw e;
    } finally {
      if (generation === dbGeneration.current) setDbBusy(false);
    }
  }
  async function loadCommands() {
    const context = projectTargetIds.current.join("|");
    const scoped = projectTargetIds.current.length
      ? targets.filter((t) => projectTargetIds.current.includes(t.id))
      : targets.filter(
          (t) =>
            t.server === target?.server &&
            t.database.replace(/_(?:A|App|S|Sys)$/i, "") ===
              target?.database.replace(/_(?:A|App|S|Sys)$/i, ""),
        );
    const sys =
      scoped.find((t) => /_S(?:ys)?$/i.test(t.database)) ??
      (projectTargetIds.current.length > 1 ? scoped[1] : undefined);
    if (!sys) {
      setError("Mở dự án có kết nối SYS để tải chức năng.");
      return;
    }
    const loaded = await request<CommandNode[]>("project.commands", {
      targetId: sys.id,
      appDatabase: defaultTarget.current?.database ?? target?.database ?? "",
    });
    if (context === projectTargetIds.current.join("|")) setCommands(loaded);
  }
  const flatCommands = (nodes: CommandNode[]): CommandNode[] =>
    nodes.flatMap((n) => [n, ...flatCommands(n.children)]);
  const matches = flatCommands(commands).filter((n) =>
    [n.entry.caption, n.entry.sysId, n.entry.commandId, n.entry.id].some((v) =>
      v.toLowerCase().includes(menuQuery.toLowerCase()),
    ),
  );
  function nextMenu() {
    const index =
      menuLast.current === menuQuery
        ? (menuIndex + 1) % Math.max(matches.length, 1)
        : 0;
    menuLast.current = menuQuery;
    setMenuIndex(index);
    setStatus(
      matches.length
        ? `Chức năng ${index + 1}/${matches.length}`
        : "Không tìm thấy chức năng",
    );
  }
  async function openController(node: CommandNode) {
    const root = workspace;
    const found = await request<{ file: string; files: string[] }>(
      "project.controller",
      { root: workspace, link: node.entry.link },
    );
    if (root !== latest.current.settings.workspace) return;
    setControllerFiles((old) => [
      ...old.filter((g) => g.name !== found.file),
      { name: found.file, files: found.files },
    ]);
    setNav("files");
    if (found.files[0]) await open(found.files[0]);
    else await open(found.file);
  }
  function exportCsv() {
    if (!result?.tables[resultIndex]) return;
    const table = result.tables[resultIndex];
    const csv = [table.columns, ...table.rows]
      .map((row) =>
        row.map((v) => '"' + (v ?? "").replaceAll('"', '""') + '"').join(","),
      )
      .join("\r\n");
    void guard(async () => {
      await request("text.export", { name: "results.csv", text: csv });
    });
  }
  async function openProject(p: Project) {
    const opened = await request<{
      project: Project;
      targets: Target[];
    }>("project.open", { id: p.id });
    setProject(opened.project);
    defaultTarget.current = opened.targets[0];
    projectTargetIds.current = opened.targets.map((t) => t.id);
    setSettings((old) => ({
      ...old,
      activeProjectId: p.id,
      workspace: opened.project.sourcePath,
      server: opened.project.server || old.server,
      database:
        opened.targets[0]?.database ?? opened.project.database ?? old.database,
      projectLastAccess: Object.assign({}, old.projectLastAccess, {
        [p.id]: new Date().toISOString(),
      }),
    }));
    setWorkspace(opened.project.sourcePath);
    setTargets((old) => [...old, ...opened.targets]);
    setCommands([]);
    const sys =
      opened.targets.find((t) => /_S(?:ys)?$/i.test(t.database)) ??
      opened.targets[1];
    if (sys)
      void request<CommandNode[]>("project.commands", {
        targetId: sys.id,
        appDatabase: opened.targets[0]?.database ?? "",
      })
        .then((loaded) => {
          if (projectTargetIds.current.includes(sys.id)) setCommands(loaded);
        })
        .catch((e) => setError(e.message));
    setObjects(undefined);
    setControllerFiles([]);
    setDialog("");
    const d = addDocument(
      {
        id: crypto.randomUUID(),
        path: null,
        name: "Query.sql",
        text: "",
        encoding: "utf-8",
        bom: false,
        newline: "CRLF",
        version: 1,
      },
      opened.targets[0],
    );
    setTabs((old) =>
      old.map((t) => (t.id === d.id ? { ...t, project: p.id } : t)),
    );
  }
  return (
    <div className="studio" data-ready={ready}>
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">LS</span>
          <strong>Studio Code</strong>
          <span className="edition">WebView</span>
        </div>
        <button
          className="command-search"
          onClick={() => setPalette("commands")}
        >
          <Search size={15} />
          <span>Tìm file, lệnh và công cụ…</span>
          <kbd>Ctrl Shift P</kbd>
        </button>
        <div className="top-actions">
          <IconButton
            label="Mở chương trình"
            onClick={() =>
              void guard(async () => {
                if (!project) throw new Error("Chọn dự án trước.");
                await request("project.program", { id: project.id });
              })
            }
          >
            <ArrowUpRight size={17} />
          </IconButton>
          <span className="connection-state">
            <i />
            {native ? ".NET Desktop" : "Xem giao diện"}
          </span>
          <IconButton
            label="Đổi theme"
            onClick={() => void guard(() => execute("theme.switch"))}
          >
            <SunMoon size={18} />
          </IconButton>
          <IconButton label="Cài đặt" onClick={() => setDialog("settings")}>
            <Settings2 size={18} />
          </IconButton>
          <IconButton
            label="Thu nhỏ cửa sổ"
            onClick={() => void guard(() => request("window.minimize"))}
          >
            <Minus size={15} />
          </IconButton>
          <IconButton
            label="Phóng to / khôi phục"
            onClick={() => void guard(() => request("window.maximize"))}
          >
            <Maximize2 size={15} />
          </IconButton>
          <IconButton
            label="Đóng cửa sổ"
            onClick={() => void guard(closeWindow)}
          >
            <X size={17} />
          </IconButton>
        </div>
      </header>
      <div className="contextbar">
        <Button variant="ghost" onClick={() => setDialog("projects")}>
          <FolderOpen size={16} />
          <span className="context-name">{project?.name ?? "Chọn dự án"}</span>
          <ChevronDown size={13} />
        </Button>
        <span className="context-divider" />
        <span className="workspace-path" title={workspace}>
          {workspace || "Chưa mở thư mục"}
        </span>
        <div className="context-spacer" />
        <Button variant="ghost" onClick={() => void guard(chooseFolder)}>
          Mở thư mục
        </Button>
        <Button variant="ghost" onClick={() => setPalette("commands")}>
          <Command size={14} />
          Lệnh
        </Button>
      </div>
      {!native && (
        <div className="preview-banner">
          Bản xem giao diện. Mở LSCode.WebView.exe để thao tác file và SQL bằng
          .NET.
        </div>
      )}
      {error && (
        <div className="error-banner" role="alert">
          <strong>Chưa hoàn thành.</strong>
          <span>{error}</span>
          <IconButton label="Đóng thông báo" onClick={() => setError("")}>
            <X size={16} />
          </IconButton>
        </div>
      )}
      <div className={`workbench ${side ? "" : "side-closed"}`}>
        <nav className="activity-rail" aria-label="Điều hướng">
          {[
            { id: "files", label: "File dự án", icon: Files },
            { id: "functions", label: "Chức năng ERP", icon: Blocks },
            { id: "database", label: "Database", icon: Database },
            { id: "search", label: "Tìm trong dự án", icon: Search },
          ].map(({ id, label, icon: Icon }) => (
            <IconButton
              key={id}
              label={label}
              aria-current={side && nav === id ? "page" : undefined}
              onClick={() => {
                if (nav === id) setSide(!side);
                else {
                  setNav(id);
                  setSide(true);
                }
              }}
            >
              <Icon size={21} />
            </IconButton>
          ))}
          <div className="rail-spacer" />
          {(
            settings.activityCommands ?? [
              "profiler",
              "compare",
              "snippets",
              "translation",
            ]
          )
            .filter(
              (id) =>
                ![
                  "workspace.files",
                  "workspace.objects",
                  "workspace.search",
                ].includes(id),
            )
            .map((id) => {
              const command = commandList.find((c) => c[0] === id);
              if (!command) return null;
              const Icon =
                (
                  {
                    profiler: Activity,
                    compare: GitCompareArrows,
                    snippets: Braces,
                    translation: Languages,
                    "file.new": FilePlus2,
                    "project.open": FolderOpen,
                    "theme.switch": SunMoon,
                  } as Record<string, typeof Activity>
                )[id] ?? Code2;
              return (
                <IconButton
                  key={id}
                  label={command[1]}
                  onClick={() => void guard(() => execute(id))}
                >
                  <Icon size={21} />
                </IconButton>
              );
            })}
        </nav>
        {side && (
          <aside className="sidebar">
            <div className="sidebar-heading">
              <h2>
                {
                  {
                    files: "Không gian dự án",
                    functions: "Chức năng ERP",
                    database: "Database",
                    search: "Tìm kiếm",
                  }[nav]
                }
              </h2>
              <IconButton
                label="Tải lại"
                onClick={() =>
                  void guard(async () => {
                    setRefresh((v) => v + 1);
                    if (nav === "functions") await loadCommands();
                    if (nav === "database") await loadObjects();
                  })
                }
              >
                <RefreshCw size={14} />
              </IconButton>
            </div>
            <div className="side-tabs">
              {[
                ["files", "File"],
                ["functions", "Chức năng"],
                ["database", "DB"],
              ].map(([id, label]) => (
                <button
                  key={id}
                  aria-selected={nav === id}
                  onClick={() => setNav(id)}
                >
                  {label}
                </button>
              ))}
            </div>
            {nav === "files" && (
              <div className="side-content">
                {workspace ? (
                  <>
                    <div className="section-label">
                      <FolderOpen size={14} />
                      <span>{workspace.split(/[\\/]/).at(-1)}</span>
                    </div>
                    <ControllerExplorer
                      groups={controllerFiles}
                      activePath={active?.path}
                      onOpen={(p) => void guard(() => open(p))}
                      onClose={(name) =>
                        setControllerFiles((old) =>
                          old.filter((g) => g.name !== name),
                        )
                      }
                    />
                  </>
                ) : (
                  <div className="side-empty">
                    <FolderOpen size={28} />
                    <p>Mở thư mục để duyệt file của bạn.</p>
                    <Button onClick={() => void guard(chooseFolder)}>
                      Mở thư mục
                    </Button>
                  </div>
                )}
              </div>
            )}
            {nav === "search" && (
              <>
                <form
                  className="side-form"
                  onSubmit={(e) => {
                    e.preventDefault();
                    void guard(find);
                  }}
                >
                  <input
                    aria-label="Nội dung tìm"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Tìm trong dự án"
                  />
                  <div className="inline-checks">
                    <label>
                      <input
                        type="checkbox"
                        checked={matchCase}
                        onChange={(e) => setMatchCase(e.target.checked)}
                      />
                      Aa
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        checked={regex}
                        onChange={(e) => setRegex(e.target.checked)}
                      />
                      Regex
                    </label>
                    <Button disabled={!workspace || searching} type="submit">
                      Tìm
                    </Button>
                    {searching && (
                      <Button
                        type="button"
                        onClick={() => void searchRef.current?.cancel()}
                      >
                        Dừng
                      </Button>
                    )}
                  </div>
                </form>
                <div className="side-content">
                  <div className="section-label">
                    {searching
                      ? "Đang tìm…"
                      : `${hits.length} kết quả${hits.length === 2000 ? " · Giới hạn 2000" : ""}`}
                  </div>
                  {hits.map((h, i) => (
                    <button
                      key={i}
                      className="search-hit"
                      onClick={() =>
                        void guard(() => open(h.path, h.line, h.column))
                      }
                    >
                      <strong>
                        {h.path.split(/[\\/]/).at(-1)}:{h.line}
                      </strong>
                      <span>{h.preview}</span>
                    </button>
                  ))}
                </div>
              </>
            )}
            {nav === "database" && (
              <>
                <div className="side-form">
                  <div className="section-label">
                    {target?.database ?? "Chưa kết nối SQL"}
                  </div>
                  <input
                    aria-label="Tên hoặc nội dung object"
                    placeholder="Tìm bảng, view, procedure"
                    value={objectQuery}
                    onChange={(e) => setObjectQuery(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") void guard(() => loadObjects());
                    }}
                  />
                  <div className="inline-checks">
                    <label>
                      <input
                        type="checkbox"
                        checked={contentSearch}
                        onChange={(e) => setContentSearch(e.target.checked)}
                      />
                      Tìm nội dung
                    </label>
                    <Button
                      disabled={dbBusy}
                      onClick={() => void guard(() => loadObjects())}
                    >
                      Tìm
                    </Button>
                  </div>
                </div>
                <div className="side-content">
                  {dbBusy ? (
                    <div className="empty">Đang tải database…</div>
                  ) : (
                    (objects?.rows.map((r, i) => (
                      <button
                        className="db-row"
                        key={i}
                        onDoubleClick={() =>
                          void guard(async () =>
                            newDocument(
                              await request<string>("schema.script", {
                                targetId: target?.id,
                                object: quoteSqlName(r[0] ?? "", r[1] ?? ""),
                              }),
                              r[1] + ".sql",
                            ),
                          )
                        }
                      >
                        <Database size={14} />
                        <span>
                          {r[0]}.{r[1]}
                          <small>{r[2]?.replaceAll("_", " ")}</small>
                        </span>
                        <ArrowUpRight size={12} />
                      </button>
                    )) ?? (
                      <div className="empty">
                        Nhấn Tìm để tải danh sách object.
                      </div>
                    ))
                  )}
                </div>
                {objects && (
                  <div className="side-footer">
                    <Button
                      disabled={objectOffset === 0 || dbBusy}
                      onClick={() =>
                        void guard(() => loadObjects(objectOffset - 200))
                      }
                    >
                      Trước
                    </Button>
                    <span>
                      {objectOffset + 1}–{objectOffset + objects.rows.length}
                    </span>
                    <Button
                      disabled={objects.rows.length < 200 || dbBusy}
                      onClick={() =>
                        void guard(() => loadObjects(objectOffset + 200))
                      }
                    >
                      Sau
                    </Button>
                  </div>
                )}
              </>
            )}
            {nav === "functions" && (
              <>
                <div className="side-form">
                  <input
                    aria-label="Tìm chức năng"
                    placeholder="Tên / sysid / mã → Enter"
                    value={menuQuery}
                    onChange={(e) => setMenuQuery(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") nextMenu();
                    }}
                  />
                  <div className="inline-checks">
                    <Button onClick={() => void guard(loadCommands)}>
                      Tải chức năng
                    </Button>
                    <Button onClick={() => setDialog("sync")}>Đồng bộ</Button>
                  </div>
                </div>
                <div className="side-content">
                  {!menuQuery && (
                    <FunctionTree
                      nodes={commands}
                      selected={selectedCommand}
                      onSelect={setSelectedCommand}
                      onOpen={(node) => void guard(() => openController(node))}
                    />
                  )}
                  {(menuQuery ? matches : []).map((n, i) => (
                    <button
                      className="search-hit"
                      key={n.entry.id}
                      aria-current={
                        menuQuery && i === menuIndex ? "page" : undefined
                      }
                      onDoubleClick={() => void guard(() => openController(n))}
                    >
                      <strong>{n.entry.caption}</strong>
                      <span>
                        {n.entry.sysId || n.entry.commandId || n.entry.id}
                      </span>
                    </button>
                  ))}
                  {commands.length === 0 && (
                    <div className="empty">Tải chức năng từ SYS của dự án.</div>
                  )}
                </div>
              </>
            )}
          </aside>
        )}
        <main className="editor-area">
          <div
            className="document-tabs"
            role="tablist"
            aria-label="File đang mở"
          >
            {tabs.map((t) => (
              <div
                className={`document-tab ${t.id === activeId ? "active" : ""}`}
                key={t.id}
              >
                <button
                  role="tab"
                  aria-selected={t.id === activeId}
                  title={t.path ?? t.name}
                  onClick={() => setActiveId(t.id)}
                >
                  <FileIcon path={t.name} />
                  <span>{t.name}</span>
                  {dirty(t) && (
                    <span className="dirty-dot" aria-label="Chưa lưu" />
                  )}
                </button>
                <IconButton
                  label={"Đóng " + t.name}
                  onClick={() => void guard(() => closeTab(t))}
                >
                  <X size={13} />
                </IconButton>
              </div>
            ))}
            <IconButton label="File mới" onClick={() => newDocument()}>
              <FilePlus2 size={16} />
            </IconButton>
          </div>
          <div className="editor-toolbar">
            <div className="breadcrumb">
              <Code2 size={14} />
              <span title={active?.path ?? ""}>
                {active?.path?.split(/[\\/]/).slice(-3).join(" / ") ??
                  active?.name ??
                  "Không gian làm việc"}
              </span>
            </div>
            <div className="toolbar-spacer" />
            {active && (
              <>
                <Select
                  label="Database của tab"
                  value={target?.id ?? ""}
                  options={[
                    { value: "", label: "Chọn kết nối" },
                    ...targets.map((t) => ({
                      value: t.id,
                      label: `${/_S(?:ys)?$/i.test(t.database) ? "SYS · " : /_A(?:pp)?$/i.test(t.database) ? "App · " : ""}${t.database} · ${t.server}`,
                    })),
                  ]}
                  onChange={(id) => {
                    if (!id) setDialog("connection");
                    else
                      setTabs((old) =>
                        old.map((t) =>
                          t.id === activeId
                            ? { ...t, target: targets.find((x) => x.id === id) }
                            : t,
                        ),
                      );
                  }}
                />
                <IconButton
                  label="Kết nối SQL"
                  onClick={() => setDialog("connection")}
                >
                  <SlidersHorizontal size={15} />
                </IconButton>
                <Button
                  variant="primary"
                  disabled={running}
                  onClick={() => void guard(runSql)}
                >
                  <Play size={14} />
                  Chạy <kbd>F5</kbd>
                </Button>
                {running && (
                  <IconButton
                    label="Dừng query"
                    onClick={() => void runRef.current?.cancel()}
                  >
                    <Square size={14} />
                  </IconButton>
                )}
                <IconButton
                  label="Lưu"
                  onClick={() => void guard(() => save())}
                >
                  <Save size={16} />
                </IconButton>
                <IconButton
                  label="Chia editor"
                  onClick={() => setSplit(!split)}
                >
                  <Columns2 size={16} />
                </IconButton>
                <IconButton
                  label="Định dạng"
                  onClick={() => void guard(format)}
                >
                  <Braces size={16} />
                </IconButton>
              </>
            )}
          </div>
          <div className={`editors ${split ? "split" : ""}`}>
            {active ? (
              <>
                {[0, ...(split ? [1] : [])].map((pane) => {
                  const doc =
                    tabs.find((t) => t.id === paneIds[pane]) ?? active;
                  return (
                    <div
                      key={pane}
                      className={
                        "editor-group " +
                        (focusedPane === pane ? "focused" : "")
                      }
                      onPointerDown={() => {
                        paneRef.current = pane;
                        setFocusedPane(pane);
                        setActiveIdRaw(doc.id);
                      }}
                    >
                      {split && (
                        <div className="pane-file-picker">
                          <Select
                            label={"File editor " + (pane + 1)}
                            value={doc.id}
                            options={tabs.map((t) => ({
                              value: t.id,
                              label: t.name + (dirty(t) ? " •" : ""),
                            }))}
                            onChange={(id) => {
                              paneRef.current = pane;
                              setFocusedPane(pane);
                              setActiveId(id);
                            }}
                          />
                        </div>
                      )}
                      <CodeEditor
                        viewId={String(pane)}
                        document={doc}
                        theme={settings.theme}
                        target={doc.target}
                        onMount={(e) => {
                          if (pane === 0) primaryEditor.current = e;
                          if (e.hasTextFocus()) {
                            editor.current = e;
                            paneRef.current = pane;
                            setFocusedPane(pane);
                            setActiveIdRaw(doc.id);
                          } else if (paneRef.current === pane)
                            editor.current = e;
                        }}
                        onCursor={setCursor}
                        onDiagnostics={setDiagnostics}
                        onChange={() => {
                          if (!suppress.current)
                            setTabs((old) =>
                              old.map((t) =>
                                t.id === doc.id
                                  ? { ...t, version: t.version + 1 }
                                  : t,
                              ),
                            );
                        }}
                      />
                    </div>
                  );
                })}
              </>
            ) : (
              <div className="welcome">
                <span className="welcome-mark">
                  <Code2 size={34} strokeWidth={1.4} />
                </span>
                <h1>Không gian làm việc</h1>
                <p>Mở dự án để sửa code và làm việc với database.</p>
                <div className="welcome-actions">
                  <Button
                    variant="primary"
                    onClick={() => setDialog("projects")}
                  >
                    <FolderOpen size={16} />
                    Chọn dự án
                  </Button>
                  <Button onClick={() => void guard(() => open())}>
                    Mở file
                  </Button>
                  <Button onClick={() => newDocument()}>SQL mới</Button>
                </div>
                <div className="welcome-shortcuts">
                  <span>
                    Mở nhanh <kbd>Ctrl P</kbd>
                  </span>
                  <span>
                    Tìm lệnh <kbd>Ctrl Shift P</kbd>
                  </span>
                  <span>
                    Chạy SQL <kbd>F5</kbd>
                  </span>
                </div>
              </div>
            )}
          </div>
          <section className={`bottom-panel ${panel ? "" : "collapsed"}`}>
            <div className="bottom-toolbar">
              <div className="panel-tabs">
                {[
                  ["results", "Kết quả"],
                  ["messages", "Thông báo"],
                  ["problems", "Vấn đề"],
                ].map(([id, label]) => (
                  <button
                    key={id}
                    aria-selected={bottom === id}
                    onClick={() => {
                      setBottom(id);
                      setPanel(true);
                    }}
                  >
                    {label}
                    {id === "problems" && diagnostics.length > 0 && (
                      <span className="count">{diagnostics.length}</span>
                    )}
                    {id === "results" && result && (
                      <span className="count">{result.tables.length}</span>
                    )}
                  </button>
                ))}
              </div>
              <div className="toolbar-spacer" />
              {panel && result && bottom === "results" && (
                <>
                  <Select
                    label="Bảng kết quả"
                    value={String(resultIndex)}
                    options={result.tables.map((t, i) => ({
                      value: String(i),
                      label: `Kết quả ${i + 1} · ${t.rows.length} dòng`,
                    }))}
                    onChange={(v) => setResultIndex(Number(v))}
                  />
                  <Button variant="ghost" onClick={exportCsv}>
                    Xuất CSV
                  </Button>
                </>
              )}
              <IconButton
                label={panel ? "Ẩn panel" : "Hiện panel"}
                onClick={() => setPanel(!panel)}
              >
                <PanelBottom size={15} />
              </IconButton>
            </div>
            {panel &&
              (bottom === "results" ? (
                running ? (
                  <div className="empty">
                    <span className="loading-dot" />
                    Đang thực thi query…
                  </div>
                ) : result?.tables[resultIndex] ? (
                  <DataGrid table={result.tables[resultIndex]} />
                ) : (
                  <div className="panel-empty">
                    <Database size={22} strokeWidth={1.5} />
                    <span>Kết quả SQL sẽ xuất hiện ở đây.</span>
                    <kbd>F5</kbd>
                  </div>
                )
              ) : bottom === "problems" ? (
                <div className="tool-list">
                  {diagnostics.length ? (
                    diagnostics.map((d, i) => (
                      <button
                        key={i}
                        onClick={() => {
                          editor.current?.setPosition({
                            lineNumber: d.line,
                            column: d.column,
                          });
                          editor.current?.revealLineInCenter(d.line);
                          editor.current?.focus();
                        }}
                      >
                        <span>
                          <strong>
                            {d.line}:{d.column}
                          </strong>{" "}
                          {d.message}
                        </span>
                      </button>
                    ))
                  ) : (
                    <div className="empty">Không phát hiện vấn đề.</div>
                  )}
                </div>
              ) : (
                <pre className="messages">
                  {error || result?.messages.join("\n") || "Chưa có thông báo."}
                </pre>
              ))}
          </section>
        </main>
      </div>
      <footer className="statusbar">
        <span className="status-indicator" />
        <span className="status-text">{status}</span>
        <span>{cursor}</span>
        <span>{active?.encoding.toUpperCase() ?? "UTF-8"}</span>
        <span>{active?.newline ?? "CRLF"}</span>
        <span>{active ? language(active.name).toUpperCase() : "LS CODE"}</span>
      </footer>
      {palette && (
        <Palette
          kind={palette}
          commands={commandList}
          workspace={workspace}
          tabs={tabs}
          onClose={() => setPalette("")}
          onCommand={(id) => {
            setPalette("");
            void guard(() => execute(id));
          }}
          onOpen={(p) => {
            setPalette("");
            void guard(() => open(p));
          }}
        />
      )}
      {dialog === "connection" && (
        <ConnectionDialog
          settings={settings}
          onClose={() => setDialog("")}
          onConnected={(t, preferences) => {
            setSettings(preferences);
            if (!project) defaultTarget.current = t;
            setTargets((old) => [...old, t]);
            setTabs((old) =>
              old.map((d) => (d.id === activeId ? { ...d, target: t } : d)),
            );
            if (!active)
              addDocument(
                {
                  id: crypto.randomUUID(),
                  path: null,
                  name: "Query.sql",
                  text: "",
                  encoding: "utf-8",
                  bom: false,
                  newline: "CRLF",
                  version: 1,
                },
                t,
              );
            setDialog("");
            setStatus("Đã kết nối " + t.database);
          }}
        />
      )}
      {dialog === "projects" && (
        <ProjectDialog
          projects={projects}
          onClose={() => setDialog("")}
          onProjects={setProjects}
          onOpen={openProject}
        />
      )}
      {dialog === "settings" && (
        <SettingsDialog
          settings={settings}
          commands={commandList}
          onClose={() => setDialog("")}
          onSave={async (next) => {
            await request("settings.save", { settings: next });
            setSettings(next);
            setDialog("");
          }}
        />
      )}
      {dialog && !["connection", "projects", "settings"].includes(dialog) && (
        <ToolDialog
          onDirtyChange={(dirty) => {
            toolDirty.current = dirty;
          }}
          kind={dialog}
          target={target}
          document={active}
          snippets={snippets}
          onSnippets={setSnippets}
          onProjects={setProjects}
          onClose={() => setDialog("")}
          selectedText={
            editor.current?.getSelection()
              ? editor.current
                  .getModel()
                  ?.getValueInRange(editor.current.getSelection()!)
              : ""
          }
          getText={() => (active ? textOf(active) : "")}
          onText={(text, name) => newDocument(text, name)}
          onInsert={(text, position) => {
            const currentEditor = editor.current;
            const selection = currentEditor?.getSelection();
            const model = currentEditor?.getModel();
            if (selection && model) {
              const offset = model.getOffsetAt(selection.getStartPosition());
              currentEditor?.executeEdits("snippet", [
                { range: selection, text, forceMoveMarkers: true },
              ]);
              currentEditor?.setPosition(
                model.getPositionAt(
                  offset +
                    Math.max(0, Math.min(position ?? text.length, text.length)),
                ),
              );
              setDialog("");
              currentEditor?.focus();
            }
          }}
          onOpen={(p) => void guard(() => open(p))}
        />
      )}
      {unsavedPrompt && (
        <Modal
          title="Lưu thay đổi trước khi đóng?"
          onClose={() => {
            unsavedPrompt.resolve("cancel");
            setUnsavedPrompt(undefined);
          }}
        >
          <p className="modal-copy">
            {unsavedPrompt.names.join(", ")} chưa lưu.
          </p>
          <div className="modal-footer">
            {(
              [
                ["cancel", "Hủy"],
                ["discard", "Đóng không lưu"],
                ["save", "Lưu thay đổi"],
              ] as const
            ).map(([choice, label]) => (
              <Button
                key={choice}
                variant={choice === "save" ? "primary" : "outline"}
                onClick={() => {
                  unsavedPrompt.resolve(choice);
                  setUnsavedPrompt(undefined);
                }}
              >
                {label}
              </Button>
            ))}
          </div>
        </Modal>
      )}
      {peek && (
        <Modal
          title={"Xem nhanh include · " + peek.document.name}
          onClose={() => {
            setPeek(undefined);
            void request("document.close", { id: peek.document.id });
          }}
          wide
        >
          <div className="tool-body large">
            <CodeEditor
              document={peek.document}
              theme={settings.theme}
              readOnly
              onChange={() => {}}
              onCursor={() => {}}
              onMount={(e) => {
                e.setPosition({ lineNumber: peek.line, column: peek.column });
                e.revealLineInCenter(peek.line);
              }}
            />
            <Button
              onClick={() => {
                setPeek(undefined);
                void guard(() =>
                  open(peek.document.path!, peek.line, peek.column),
                );
              }}
            >
              Mở file trong editor
            </Button>
          </div>
        </Modal>
      )}
      {external && (
        <Modal
          title="File đã thay đổi bên ngoài"
          onClose={() => setExternal(undefined)}
        >
          <p className="modal-copy">
            {external.name} có phiên bản mới trên ổ đĩa.
            {dirty(external)
              ? " Tab đang có thay đổi chưa lưu. Tải lại sẽ bỏ nội dung hiện tại."
              : ""}
          </p>
          <div className="modal-footer">
            <Button onClick={() => setExternal(undefined)}>Giữ nội dung</Button>
            <Button
              variant="primary"
              onClick={() =>
                void guard(async () => {
                  const d = await request<DocumentInfo>("document.open", {
                    path: external.path,
                  });
                  modelFor(d);
                  setTabs((old) =>
                    old.map((t) =>
                      t.id === external.id
                        ? { ...d, saved: d.text, target: t.target }
                        : t,
                    ),
                  );
                  setActiveId(d.id);
                  requestAnimationFrame(() => {
                    disposeModel(external);
                    void request("document.close", { id: external.id }).catch(
                      () => {},
                    );
                  });
                  setExternal(undefined);
                })
              }
            >
              Tải lại
            </Button>
          </div>
        </Modal>
      )}
    </div>
  );
}
type CommandNode = {
  entry: {
    id: string;
    caption: string;
    sysId: string;
    commandId: string;
    link: string;
  };
  children: CommandNode[];
};
function FunctionTree({
  nodes,
  selected,
  onSelect,
  onOpen,
}: {
  nodes: CommandNode[];
  selected: string;
  onSelect: (id: string) => void;
  onOpen: (node: CommandNode) => void;
}) {
  const [expanded, setExpanded] = useState(new Set<string>());
  return (
    <ul className="file-tree">
      {nodes.map((node) =>
        node.entry.caption.trim() === "-" ? (
          <li
            key={node.entry.id}
            role="separator"
            className="function-separator"
          />
        ) : (
          <li key={node.entry.id}>
            <button
              className="tree-row"
              aria-current={selected === node.entry.id ? "page" : undefined}
              aria-expanded={
                node.children.length ? expanded.has(node.entry.id) : undefined
              }
              title={[
                node.entry.caption,
                node.entry.sysId,
                node.entry.commandId,
              ]
                .filter(Boolean)
                .join(" · ")}
              onClick={() => {
                onSelect(node.entry.id);
                if (node.children.length)
                  setExpanded((old) => {
                    const next = new Set(old);
                    if (next.has(node.entry.id)) next.delete(node.entry.id);
                    else next.add(node.entry.id);
                    return next;
                  });
              }}
              onDoubleClick={() => {
                if (node.entry.link) onOpen(node);
              }}
            >
              {node.children.length ? (
                expanded.has(node.entry.id) ? (
                  <ChevronDown size={12} />
                ) : (
                  <ChevronRight size={12} />
                )
              ) : (
                <span className="tree-spacer" />
              )}
              <span>{node.entry.caption}</span>
            </button>
            {node.children.length > 0 && expanded.has(node.entry.id) && (
              <div className="tree-children">
                <FunctionTree
                  nodes={node.children}
                  selected={selected}
                  onSelect={onSelect}
                  onOpen={onOpen}
                />
              </div>
            )}
          </li>
        ),
      )}
    </ul>
  );
}
function FileTree({
  path,
  onOpen,
  activePath,
  refresh,
  level = 0,
}: {
  path: string;
  onOpen: (p: string) => void;
  activePath?: string | null;
  refresh: number;
  level?: number;
}) {
  const [entries, setEntries] = useState<Entry[]>([]),
    [expanded, setExpanded] = useState(new Set<string>()),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(true);
  useEffect(() => {
    let current = true;
    setLoading(true);
    setError("");
    request<Entry[]>("workspace.list", { path })
      .then((data) => {
        if (current) setEntries(data);
      })
      .catch((e) => {
        if (current) setError(e.message);
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [path, refresh]);
  if (loading) return <p className="tree-message">Đang tải…</p>;
  if (error) return <p className="tree-message error-text">{error}</p>;
  if (entries.length === 0)
    return <p className="tree-message">Thư mục trống</p>;
  return (
    <ul className="file-tree">
      {entries.map((e) => (
        <li key={e.path}>
          <button
            className="tree-row"
            aria-expanded={e.directory ? expanded.has(e.path) : undefined}
            aria-current={activePath === e.path ? "page" : undefined}
            title={e.name}
            onClick={() => {
              if (e.directory)
                setExpanded((old) => {
                  const next = new Set(old);
                  if (next.has(e.path)) next.delete(e.path);
                  else next.add(e.path);
                  return next;
                });
              else onOpen(e.path);
            }}
          >
            {e.directory ? (
              expanded.has(e.path) ? (
                <ChevronDown size={12} />
              ) : (
                <ChevronRight size={12} />
              )
            ) : (
              <span className="tree-spacer" />
            )}
            {e.directory ? <Folder size={15} /> : <FileCode2 size={15} />}
            <span>
              {e.name.length > 30
                ? e.name.slice(0, 18) + "…" + e.name.slice(-11)
                : e.name}
            </span>
          </button>
          {e.directory && expanded.has(e.path) && (
            <div
              className="tree-children"
              style={{ marginLeft: level > 3 ? 6 : 14 }}
            >
              <FileTree
                path={e.path}
                level={level + 1}
                onOpen={onOpen}
                activePath={activePath}
                refresh={refresh}
              />
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}
function Palette({
  kind,
  commands,
  workspace,
  tabs,
  onCommand,
  onOpen,
  onClose,
}: {
  kind: string;
  commands: string[][];
  workspace: string;
  tabs: Tab[];
  onCommand: (id: string) => void;
  onOpen: (p: string) => void;
  onClose: () => void;
}) {
  const [query, setQuery] = useState(""),
    [files, setFiles] = useState<string[]>(
      tabs.filter((t) => t.path).map((t) => t.path!),
    );
  useEffect(() => {
    if (kind === "files" && workspace)
      void request<string[]>("workspace.files", { path: workspace })
        .then(setFiles)
        .catch(() => {});
  }, [kind, workspace]);
  return (
    <Modal
      title={kind === "files" ? "Mở nhanh file" : "Bảng lệnh"}
      onClose={onClose}
    >
      <input
        autoFocus
        className="palette-input"
        aria-label="Tìm lệnh hoặc file"
        placeholder={kind === "files" ? "Tên file…" : "Nhập tên lệnh…"}
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />
      <div className="palette-list">
        {kind === "files"
          ? files
              .filter((f) => f.toLowerCase().includes(query.toLowerCase()))
              .slice(0, 100)
              .map((f) => (
                <button key={f} onClick={() => onOpen(f)}>
                  <FileCode2 size={16} />
                  <span>
                    {f.split(/[\\/]/).at(-1)}
                    <small>{f}</small>
                  </span>
                </button>
              ))
          : commands
              .filter(([, label]) =>
                label.toLocaleLowerCase().includes(query.toLocaleLowerCase()),
              )
              .map(([id, label, key]) => (
                <button key={id} onClick={() => onCommand(id)}>
                  <span>{label}</span>
                  <kbd>{key}</kbd>
                </button>
              ))}
      </div>
    </Modal>
  );
}
