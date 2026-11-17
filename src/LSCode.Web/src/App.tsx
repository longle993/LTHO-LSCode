import ControllerExplorer, { FileIcon } from "./ControllerExplorer";
import { SourceCollectionDialog } from "./features/SourceCollectionDialog";
import { pathIsWithin, rebasePath, tabFolderLabel } from "./fileLabels";
import { FileSections } from "./features/FileSections";
import { ControllerPreview } from "./features/ControllerPreview";
import { ProjectHistory } from "./features/ProjectHistory";
import { EmployeeFunctionTabs } from "./features/EmployeeFunctionTabs";
import logo from "../../../assets/logo2.png";
import { LoadingLine } from "./LoadingLine";
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
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
  PanelLeftClose,
  X,
  Command,
  RefreshCw,
  Trash2,
  Activity,
  GitCompareArrows,
  Blocks,
  Languages,
  ArrowUpRight,
  NotebookPen,
} from "lucide-react";
import { native, request, operation, subscribe, quoteSqlName, notifyWindowReady, writeClipboard } from "./bridge";
import { PanelResizeHandle, WorkspaceResizeHandle } from "./PanelResizeHandle";
import { ProfilerTool } from "./features/ProfilerTool";
import { TableTool, TableToolErrorBoundary } from "./features/TableTool";
import { TemplateStudio } from "./features/TemplateStudio";
import { SqlWatchTool } from "./features/SqlWatchTool";
import { MenuEditor } from "./features/MenuEditor";
import { AdvanceNote, type AdvanceNoteDataCapture, type AdvanceNoteMenu } from "./features/AdvanceNote";
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
import { commands as commandList, keyboardGesture, migratePreferences } from "./commands";
import {
  CodeEditor,
  modelFor,
  textOf,
  disposeModel,
  language,
  markSqlErrors,
} from "./editor";
import type { EditorDiagnostic, EditorHandle } from "./editor";
import { Button, IconButton, Select, Modal, DataGrid } from "./ui";
import {
  ToolDialog,
  ProjectDialog,
  ConnectionDialog,
  SettingsDialog,
} from "./tools";

type Tab = DocumentInfo & { saved: string; target?: Target; project?: string };
type TableTab = { id: string; number: number; target?: Target; table?: string };
type ControllerGroup = { id: string; link: string; file: string; files: string[]; employeeTab?: boolean; sysId?: string };
const employeeController = "hrpigeneralinformation";
function isEmployeeController(group: ControllerGroup) {
  return group.files.some((path) => /(?:^|[\\/])hrpigeneralinformation\.(xml|f)$/i.test(path));
}
type Entry = { path: string; name: string; directory: boolean };
type Hit = { path: string; line: number; column: number; preview: string };
type EditorSearchHit = {
  line: number;
  column: number;
  endColumn: number;
  preview: string;
};
type FormatLanguage = "sql" | "json" | "xml";
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
  const [diagnostics, setDiagnostics] = useState<EditorDiagnostic[]>([]);
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
  const [profilerTab, setProfilerTab] = useState<{ target?: Target }>(),
    [tableTabs, setTableTabs] = useState<TableTab[]>([]),
    [selectedTool, setSelectedTool] = useState<string>(),
    [profilerBusy, setProfilerBusy] = useState(false),
    [closingProfiler, setClosingProfiler] = useState(false);
  const [watchTab, setWatchTab] = useState<{ target?: Target }>();
  const watchBusy = useRef(false);
  function closeWatchTab() {
    if (watchBusy.current) { setStatus("Dừng Watch trước khi đóng tab."); return; }
    setWatchTab(undefined);
    setSelectedTool(current => current === "sql-watch" ? undefined : current);
  }
  const [formatMenuOpen, setFormatMenuOpen] = useState(false);
  const formatMenuRef = useRef<HTMLDivElement>(null);
  const profilerSelected = selectedTool === "profiler";
  const [studioOpen, setStudioOpen] = useState(false);
  const studioDirty = useRef(false);
  const studioBusy = useRef(false);
  const [advanceNoteOpen, setAdvanceNoteOpen] = useState(false);
  const [advanceMenuPick, setAdvanceMenuPick] = useState<(AdvanceNoteMenu & { sequence: number })>();
  const [advanceDataCapture, setAdvanceDataCapture] = useState<AdvanceNoteDataCapture>();
  const advanceDataSequence = useRef(0);
  const advanceNoteDirty = useRef(false);
  function closeAdvanceNote() {
    if (advanceNoteDirty.current && !window.confirm("Bỏ thay đổi Generate Update chưa lưu?")) return;
    advanceNoteDirty.current = false;
    setAdvanceNoteOpen(false);
    setSelectedTool(current => current === "advance-note" ? undefined : current);
  }
  const tableTab = tableTabs.find((tab) => tab.id === selectedTool);
  const tableSelected = !!tableTab;
  const tableNumber = useRef(0);
  useEffect(() => {
    if (selectedTool) document.getElementById(selectedTool + "-tab")?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [selectedTool]);
  useEffect(() => {
    setFormatMenuOpen(false);
  }, [activeId, selectedTool]);
  useEffect(() => {
    if (!formatMenuOpen) return;
    const closeOutside = (event: PointerEvent) => {
      if (!formatMenuRef.current?.contains(event.target as Node)) setFormatMenuOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setFormatMenuOpen(false);
        formatMenuRef.current?.querySelector<HTMLButtonElement>("button")?.focus();
      }
    };
    document.addEventListener("pointerdown", closeOutside);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOutside);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [formatMenuOpen]);
  const setActiveId = (id: string) => {
    setSelectedTool(undefined);
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
    [openingInitialProject, setOpeningInitialProject] = useState(!!initialProjectId),
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
    [controllerRequests, setControllerRequests] = useState(0),
    [controllers, setControllers] = useState<ControllerGroup[]>([]),
    [expandedController, setExpandedController] = useState<string>();
  const controller = controllers.find((entry) => entry.id === expandedController);
  const controllerRequest = useRef(0);
  const controllerSelection = useRef(0);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [history, setHistory] = useState<{ projectId: string; files: string[] }[]>();
  const [sourceCollection, setSourceCollection] = useState<{ samples: string[]; destination: string }>();
  const [historySearch, setHistorySearch] = useState("");
  const historyResults = useMemo(() => {
    const query = historySearch.trim().toLowerCase();
    if (!query) return history;
    return history?.flatMap((entry) => {
      const files = entry.files.filter((path) =>
        path.split(/[\\/]/).slice(-2).some((part) => part.toLowerCase().includes(query)));
      return files.length ? [{ ...entry, files }] : [];
    });
  }, [history, historySearch]);
  const switchingHistoryProject = useRef(false);
  const [projectActionBusy, setProjectActionBusy] = useState(false);
  const [structureClear, setStructureClear] = useState<{ id: string; path: string; count: number }>();
  const [searchFolder, setSearchFolder] = useState("");
  const searchFolders = useMemo(() => [...new Set(controller?.files.flatMap((path) => {
    const separator = Math.max(path.lastIndexOf("\\"), path.lastIndexOf("/"));
    return separator > 0 ? [path.slice(0, separator)] : [];
  }) ?? [])].sort(), [controller]);
  const searchRoot = searchFolders.includes(searchFolder) ? searchFolder : workspace;
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
  const [external, setExternal] = useState<Tab>();
  const primaryEditor = useRef<EditorHandle>();
  const scriptToFocus = useRef<string>();
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
    dbLoadedQuery = useRef(""),
    dbRequest = useRef<ReturnType<typeof operation<Table>>>(),
    dbTimer = useRef<number>(),
    dbCache = useRef(new Map<string, { table: Table; expires: number }>()),
    controllerCache = useRef(new Map<string, { file: string; files: string[] }>()),
    defaultTarget = useRef<Target>(),
    projectSysTarget = useRef<Target>(),
    projectTargetIds = useRef<string[]>([]),
    pendingOpens = useRef(new Map<string, Promise<void>>());
  latest.current = { tabs, activeId, settings };
  const active = tabs.find((t) => t.id === activeId),
    target = active?.target ?? defaultTarget.current;
  const [editorSearchQuery, setEditorSearchQuery] = useState("");
  const editorSearch = useMemo(() => {
    const query = editorSearchQuery;
    if (!active || query.length === 0) return { hits: [] as EditorSearchHit[], truncated: false };

    const needle = query.toLowerCase();
    const lines = textOf(active).split(/\r?\n/);
    const hits: EditorSearchHit[] = [];
    const limit = 200;
    for (let index = 0; index < lines.length; index++) {
      const line = lines[index];
      const column = line.toLowerCase().indexOf(needle);
      if (column < 0) continue;
      hits.push({
        line: index + 1,
        column: column + 1,
        endColumn: column + query.length + 1,
        preview: line,
      });
      if (hits.length === limit)
        return { hits, truncated: true };
    }
    return { hits, truncated: false };
  }, [active?.id, active?.version, editorSearchQuery]);

  const focusEditorSearchHit = useCallback((hit: EditorSearchHit) => {
    const currentEditor = editor.current;
    const model = currentEditor?.getModel();
    if (!currentEditor || !model || !active || model.uri.toString() !== `lscode://document/${active.id}`)
      return;
    currentEditor.setSelection({
      startLineNumber: hit.line,
      startColumn: hit.column,
      endLineNumber: hit.line,
      endColumn: hit.endColumn,
    });
    currentEditor.revealLineInCenter(hit.line);
    currentEditor.focus();
  }, [active?.id]);
  const [selectedCommand, setSelectedCommand] = useState("");
  const [menuEditor, setMenuEditor] = useState<{ command: CommandNode; target: Target; root: string }>();
  const menuDirty = useRef(false);
  const menuBusy = useRef(false);
  function closeMenuEditor() {
    if (menuBusy.current || (menuDirty.current && !window.confirm("Bỏ thay đổi menu chưa lưu?"))) return;
    setMenuEditor(undefined);
    menuDirty.current = false;
    setSelectedTool(current => current === "menu-editor" ? undefined : current);
  }
  function openMenuEditor(command: CommandNode) {
    if (!projectSysTarget.current) return;
    if (menuEditor?.command.entry.id === command.entry.id && menuEditor.target.id === projectSysTarget.current.id) {
      setSelectedTool("menu-editor"); return;
    }
    if (menuBusy.current || (menuDirty.current && !window.confirm("Bỏ thay đổi menu chưa lưu để mở menu khác?"))) return;
    menuDirty.current = false;
    setMenuEditor({ command, target: projectSysTarget.current, root: workspace });
    setSelectedTool("menu-editor");
  }
  useEffect(() => {
    setObjects(undefined);
    dbLoadedQuery.current = "";
    setObjectOffset(0);
  }, [target?.id]);
  useEffect(() => {
    if (!side || nav !== "database" || !target) return;
    const key = dbQueryKey();
    if (dbLoadedQuery.current !== key) {
      if (!dbLoadedQuery.current && !objectQuery.trim()) void guard(() => loadObjects());
      else dbTimer.current = window.setTimeout(() => void guard(() => loadObjects()), contentSearch ? 500 : 280);
    }
    return () => {
      window.clearTimeout(dbTimer.current);
      dbGeneration.current++;
      void dbRequest.current?.cancel().catch(() => {});
      dbRequest.current = undefined;
      setDbBusy(false);
    };
  }, [side, nav, target?.id, objectQuery, contentSearch]);
  useEffect(() => {
    controllerCache.current.clear();
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
  const dirtyByTab = useRef(new WeakMap<Tab, boolean>());
  const tabDetails = useMemo(() => {
    const groups = new Map<string, Tab[]>();
    for (const tab of tabs) {
      const name = tab.name.toLowerCase();
      const peers = groups.get(name) ?? [];
      peers.push(tab);
      groups.set(name, peers);
    }
    return new Map(tabs.map((tab) => {
      const folder = tabFolderLabel(tab, groups.get(tab.name.toLowerCase())!);
      const changed = dirtyByTab.current.get(tab) ?? dirty(tab);
      dirtyByTab.current.set(tab, changed);
      return [tab.id, { folder, label: folder ? tab.name + " — " + folder : tab.name, changed }];
    }));
  }, [tabs]);
  const paneOptions = useMemo(() => tabs.map((tab) => {
    const detail = tabDetails.get(tab.id)!;
    return { value: tab.id, label: detail.label + (detail.changed ? " •" : "") };
  }), [tabs, tabDetails]);
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
  useEffect(() => {
    if (!native || !active?.path || selectedTool) return;
    const path = active.path;
    const candidates = projects.filter((entry) => entry.sourcePath && pathIsWithin(path, entry.sourcePath));
    const owner = candidates.find((entry) => entry.id === active.project)
      ?? candidates.find((entry) => entry.id === project?.id)
      ?? candidates.sort((a, b) => b.sourcePath.length - a.sourcePath.length)[0];
    if (!owner) return;
    let frame = requestAnimationFrame(() => {
      frame = requestAnimationFrame(() => {
        if (editor.current?.getModel()?.uri.path !== "/" + active.id) return;
        void guard(async () => {
          await request("project.history.record", { id: owner.id, path });
          setHistoryRevision((version) => version + 1);
        });
      });
    });
    return () => cancelAnimationFrame(frame);
  }, [active?.id, active?.path, selectedTool, project?.id, projects, guard]);
  useEffect(() => {
    if (!native || nav !== "history") return;
    let current = true;
    void request<NonNullable<typeof history>>("project.history.list").then((entries) => {
      if (current) setHistory(entries);
    }).catch((error) => { if (current) setError((error as Error).message); });
    return () => { current = false; };
  }, [nav, projects, historyRevision]);
  const addDocument = useCallback((d: DocumentInfo & { project?: string }, t?: Target) => {
    setTabs((old) => [...old, { ...d, saved: d.text, target: t }]);
    setActiveId(d.id);
    return d;
  }, []);
  const newDocument = useCallback(
    (text = "", name?: string, connection?: Target) =>
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
        connection ?? defaultTarget.current ??
          latest.current.tabs.find((t) => t.id === latest.current.activeId)
            ?.target ??
          targets[0],
      ),
    [addDocument, targets],
  );
  const open = useCallback(
    async (path?: string, line?: number, column?: number, projectId?: string) => {
      if (!path) {
        const documents = await request<DocumentInfo[]>("document.openMany");
        const byPath = new Map(latest.current.tabs.flatMap((tab) =>
          tab.path ? [[tab.path.toLowerCase(), tab.id] as const] : []));
        const added: Tab[] = [];
        const redundant: string[] = [];
        let lastId: string | undefined;
        for (const document of documents) {
          const key = document.path?.toLowerCase();
          const existing = key ? byPath.get(key) : undefined;
          if (existing) {
            lastId = existing;
            redundant.push(document.id);
          } else {
            lastId = document.id;
            if (key) byPath.set(key, document.id);
            added.push({ ...document, saved: document.text, target: defaultTarget.current ?? targets[0] });
          }
        }
        if (added.length) setTabs((old) => [...old, ...added]);
        if (lastId) setActiveId(lastId);
        await Promise.all(redundant.map((id) => request("document.close", { id })));
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
          if (d) addDocument({ ...d, project: projectId }, defaultTarget.current ?? (projectId ? undefined : targets[0]));
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
    if (watchBusy.current) { setStatus("Dừng Watch trước khi đóng ứng dụng."); return; }
    if (menuBusy.current) { setStatus("Menu đang xử lý. Thử đóng lại khi hoàn tất."); return; }
    if (menuDirty.current && !window.confirm("Menu ERP còn thay đổi chưa lưu. Đóng ứng dụng?")) return;
    if (studioBusy.current) { setStatus("Studio đang xử lý. Thử đóng lại khi hoàn tất."); return; }
    if (studioDirty.current && !window.confirm("Dynamic Template Studio còn thay đổi chưa lưu. Đóng ứng dụng?"))
      return;
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
        else if (e.type === "files.dropped" && e.paths?.length) {
          void guard(async () => {
            for (const path of e.paths!) await open(path);
            setStatus(`Đã mở ${e.paths!.length} file được kéo vào.`);
          });
        }
      }),
    [closeWindow, guard, open],
  );
  useEffect(() => {
    const bridge = window.chrome?.webview;
    if (!bridge?.postMessageWithAdditionalObjects) return;
    const containsFiles = (event: DragEvent) => Array.from(event.dataTransfer?.types ?? []).includes("Files");
    const dragOver = (event: DragEvent) => {
      if (!containsFiles(event)) return;
      event.preventDefault();
      if (event.dataTransfer) event.dataTransfer.dropEffect = "copy";
    };
    const drop = (event: DragEvent) => {
      if (!containsFiles(event)) return;
      event.preventDefault();
      const files = Array.from(event.dataTransfer?.files ?? []).slice(0, 100);
      if (files.length) bridge.postMessageWithAdditionalObjects!({ type: "files.drop" }, files);
    };
    window.addEventListener("dragover", dragOver);
    window.addEventListener("drop", drop);
    return () => {
      window.removeEventListener("dragover", dragOver);
      window.removeEventListener("drop", drop);
    };
  }, []);
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
        setTabs((old) => [...old, { ...d, saved: "" }]);
        setActiveId(d.id);
      }
      setReady(true);
      if (initialProjectId) {
        const selected = b.projects.find((p) => p.id === initialProjectId);
        if (selected) await openProject(selected);
      }
      setStatus(
        recovery.length
          ? `Đã khôi phục ${recovery.length} file chưa lưu`
          : "Đã kết nối .NET",
      );
    }).finally(() => setOpeningInitialProject(false));
  }, [guard]);
  useEffect(() => {
    if (native && window.__LSCODE_SMOKE__ && ready) return notifyWindowReady();
  }, [ready]);
  useEffect(() => {
    const root = document.documentElement;
    root.dataset.theme = settings.theme;
    const color = settings.accentColor;
    if (color && /^#[\da-f]{6}$/i.test(color)) {
      const [r, g, b] = color.slice(1).match(/../g)!.map((value) => {
        const channel = parseInt(value, 16) / 255;
        return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
      });
      root.style.setProperty("--primary", color);
      root.style.setProperty("--accent", color);
      const light = 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.179;
      root.style.setProperty("--primary-hover", light
        ? "color-mix(in srgb, var(--primary) 88%, white)"
        : "color-mix(in srgb, var(--primary) 88%, black)");
      root.style.setProperty("--primary-foreground", light ? "#000" : "#fff");
    } else {
      for (const property of ["--primary", "--accent", "--primary-hover", "--primary-foreground"]) root.style.removeProperty(property);
    }
  }, [settings.theme, settings.accentColor]);
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
    let nextBackground = 0;
    const timer = setInterval(async () => {
      if (busy) return;
      busy = true;
      try {
        const { tabs, activeId } = latest.current;
        const eligible = tabs.filter((tab) => tab.path && !notified.current.has(tab.id));
        const active = eligible.find((tab) => tab.id === activeId);
        const background = eligible.filter((tab) => tab.id !== activeId);
        const batch = active ? [active] : [];
        for (let index = 0; index < Math.min(3, background.length); index++) {
          batch.push(background[nextBackground++ % background.length]);
        }
        for (const t of batch) {
          if (await request<boolean>("document.changed", { id: t.id })) {
            const current = latest.current.tabs.find((tab) => tab.id === t.id);
            if (!current || current.path !== t.path) continue;
            notified.current.add(t.id);
            setExternal(current);
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
        const smokeNote = {
          id: "smoke-note", title: "Kiểm tra Generate Update", status: "Mới", details: "Smoke",
          menuId: "AA.00.00", menuCaption: "Smoke menu", sysId: "SmokeController", link: "Smoke.aspx",
          sourceFiles: [workspace ? workspace + "\\fixture.xml" : "fixture.xml"], updatedAt: new Date().toISOString(),
        };
        await request("advanceNote.save", { projectId: "smoke-project", items: [smokeNote] });
        const loadedNotes = await request<{ items: Array<typeof smokeNote> }>("advanceNote.load", { projectId: "smoke-project" });
        if (loadedNotes.items[0]?.menuId !== smokeNote.menuId ||
            !document.querySelector('button[aria-label="Generate Update"]'))
          throw new Error("Generate Update bridge or entry point");
        await request("sourceCollection.sync", { group: "HRM" });
        setSourceCollection({ samples: [], destination: workspace ?? "" });
        const waitFor = async <T,>(read: () => T | null | undefined): Promise<T> => {
          for (let attempt = 0; attempt < 100; attempt++) {
            const value = read();
            if (value) return value;
            await new Promise(resolve => window.setTimeout(resolve, 20));
          }
          throw new Error("Source Collection did not finish rendering");
        };
        try {
          const selector = await waitFor(() => {
            const button = document.querySelector<HTMLButtonElement>('.source-collection-modal button[aria-label="Phiên bản"]');
            return button && !button.disabled ? button : null;
          });
          selector.click();
          const popup = await waitFor(() => document.querySelector<HTMLElement>(".source-collection-modal :popover-open"));
          await new Promise(resolve => window.setTimeout(resolve, 230));
          const rect = popup.getBoundingClientRect();
          const modal = popup.closest("dialog")!.getBoundingClientRect();
          const outsideY = rect.bottom > modal.bottom + 12 ? rect.bottom - 12 : rect.top + 12;
          if ((outsideY >= modal.top && outsideY <= modal.bottom) ||
              !popup.contains(document.elementFromPoint(rect.left + 12, outsideY)))
            throw new Error("Source Collection dropdown is clipped by its dialog");
        } finally {
          setSourceCollection(undefined);
        }
        newDocument(
          "-- Kiểm tra WebView2\r\nSELECT 1 AS [Giá trị];",
          "webview-smoke.sql",
        );
        setProject({
          id: "smoke-project", name: "Smoke ERP", server: "", database: "", sourcePath: workspace,
          code: "SMOKE", title: "Smoke ERP", programPath: "", active: true,
        });
        const sqlRunButton = await waitFor(() => document.querySelector<HTMLButtonElement>(".editor-toolbar .sql-run-button"));
        const toolbarLabels = Array.from(document.querySelectorAll<HTMLButtonElement>(".editor-toolbar > button"))
          .map(button => button.textContent?.trim());
        if (toolbarLabels.includes("Generate Update") || toolbarLabels.includes("SQL Debug / Watch") ||
            getComputedStyle(sqlRunButton).whiteSpace !== "nowrap")
          throw new Error("Editor toolbar responsive actions");
        setAdvanceNoteOpen(true);
        setSelectedTool("advance-note");
        const advancePanel = await waitFor(() => document.querySelector<HTMLElement>(".advance-note"));
        await waitFor(() => advancePanel.querySelector<HTMLInputElement>('input[value="Kiểm tra Generate Update"]'));
        if (advancePanel.scrollWidth > advancePanel.clientWidth + 1 ||
            !advancePanel.querySelector('.advance-note-source-search') ||
            !advancePanel.querySelector('.advance-note-file-path') ||
            advancePanel.querySelector('[aria-label="Trạng thái"]') ||
            Array.from(advancePanel.querySelectorAll("button")).some(button => button.textContent?.includes("Chọn chức năng")))
          throw new Error("Generate Update workspace overflows horizontally");
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
            "Generate Update XML bridge and entry point",
            "Generate Update source search, full-width File Path and SQL tabs render",
            "Editor toolbar keeps SQL actions readable while resizing",
            "Source Collection dropdown escapes dialog clipping",
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
  async function format(kind?: FormatLanguage) {
    if (!active) return;
    const formatted = await request<string>("text.format", {
      text: textOf(active),
      language: kind ?? language(active.name),
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
      controllerRequest.current++;
      setControllers([]);
      setExpandedController(undefined);
      setNav("files");
      const next = { ...settings, workspace: path };
      setSettings(next);
      await request("settings.save", { settings: next });
    }
  }
  async function closeProfilerTab() {
    if (profilerBusy || closingProfiler) return;
    setClosingProfiler(true);
    try {
      await request("profiler.stop");
      setProfilerTab(undefined);
      setSelectedTool((current) => current === "profiler" ? undefined : current);
    } finally {
      setClosingProfiler(false);
    }
  }
  const execute = async (id: string) => {
    if (selectedTool && /^(editor\.|sql\.|file\.save|xml\.include)/.test(id)) return;
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
        if (selectedTool === "sql-watch") closeWatchTab();
        else if (selectedTool === "menu-editor") closeMenuEditor();
        else if (selectedTool === "advance-note") closeAdvanceNote();
        else if (profilerSelected) await closeProfilerTab();
        else if (selectedTool === "template-studio") closeStudio();
        else if (tableTab) closeTableTab(tableTab.id);
        else if (active) await closeTab(active);
        break;
      case "profiler":
        setProfilerTab((existing) => existing ?? { target });
        setSelectedTool("profiler");
        break;
      case "template-studio":
        setStudioOpen(true);
        setSelectedTool(id);
        break;
      case "advance-note":
        if (!project) { setDialog("projects"); break; }
        setAdvanceNoteOpen(true);
        setSelectedTool(id);
        break;
      case "table": {
        const id = "table-" + crypto.randomUUID();
        const number = ++tableNumber.current;
        setTableTabs((tabs) => [...tabs, { id, number, target }]);
        setSelectedTool(id);
        break;
      }
      case "window.new":
        await request("window.new");
        break;
      case "editor.find":
        editor.current?.trigger("keyboard", "actions.find", null);
        break;
      case "editor.sections":
        if (active) setDialog("editor.sections");
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
        controllerCache.current.clear();
        if (controller) await openController(controller.link, controller.employeeTab, controller.sysId);
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
        if (tableTab) {
          if (scoped.length < 2) {
            setStatus("Dự án cần kết nối App và Sys.");
            return;
          }
          const currentId = tableTab.target?.id;
          const next = scoped[(scoped.findIndex((entry) => entry.id === currentId) + 1) % scoped.length];
          setTableTabs((current) => current.map((entry) => entry.id === tableTab.id ? { ...entry, target: next } : entry));
          setStatus(`Đã chuyển Cấu trúc và dữ liệu sang ${/_S(?:ys)?$/i.test(next.database) ? "Sys" : "App"}.`);
          return;
        }
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
  function closeTableTab(id: string) {
    const index = tableTabs.findIndex((tab) => tab.id === id);
    const next = tableTabs[index + 1] ?? tableTabs[index - 1];
    setTableTabs((tabs) => tabs.filter((tab) => tab.id !== id));
    setSelectedTool((current) => current === id ? next?.id : current);
  }
  function closeStudio() {
    if (studioBusy.current) { setStatus("Studio đang xử lý. Thử đóng lại khi hoàn tất."); return; }
    if (studioDirty.current && !window.confirm("Bỏ thay đổi chưa lưu trong Studio?")) return;
    studioDirty.current = false;
    setStudioOpen(false);
    setSelectedTool((current) => current === "template-studio" ? undefined : current);
  }
  const executeRef = useRef(execute);
  executeRef.current = execute;
  useEffect(() => {
    const keys = (e: KeyboardEvent) => {
      if (e.isComposing) return;
      const key = keyboardGesture(e);
      if (e.target instanceof HTMLElement &&
        (key === "Ctrl+Space" && e.target.closest('.table-inspection [role="combobox"]') ||
          key === "F2" && e.target.closest(".project-file-tree, .table-data-grid") ||
          key === "Ctrl+S" && e.target.closest(".table-inspection"))) return;
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
        (id === "table" && key.toUpperCase() === "CTRL+SHIFT+T") ||
        (latest.current.settings.shortcuts[id] ?? [shortcut]).some(
          (v) => v.toUpperCase() === key.toUpperCase(),
        ),
      );
      if (c && (c[0] === "window.new" || !document.querySelector("dialog[open]"))) {
        e.preventDefault();
        e.stopImmediatePropagation();
        if (c[0] !== "window.new" || !e.repeat) void guard(() => executeRef.current(c[0]));
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
      path: searchRoot,
      query: search,
      regex,
      matchCase,
      buffers: Object.fromEntries(
        tabs.filter((t) => t.path && pathIsWithin(t.path, searchRoot)).map((t) => [t.path!, textOf(t)]),
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
  function dbQueryKey() {
    return JSON.stringify([target?.id, objectQuery.trim(), contentSearch]);
  }
  async function loadObjects(offset = 0, refresh = false) {
    if (!target) {
      setDialog("connection");
      return;
    }
    window.clearTimeout(dbTimer.current);
    const generation = ++dbGeneration.current;
    void dbRequest.current?.cancel().catch(() => {});
    dbRequest.current = undefined;
    const queryKey = dbQueryKey();
    const cacheKey = JSON.stringify([queryKey, offset]);
    const cached = dbCache.current.get(cacheKey);
    if (!refresh && cached && cached.expires > Date.now()) {
      dbLoadedQuery.current = queryKey;
      setObjects(cached.table);
      setObjectOffset(offset);
      setDbBusy(false);
      return;
    }
    setDbBusy(true);
    try {
      const op = operation<Table>("schema.objects", {
        targetId: target.id,
        query: objectQuery.trim(),
        content: contentSearch,
        offset,
      });
      dbRequest.current = op;
      const table = await op.promise;
      if (generation === dbGeneration.current) {
        dbLoadedQuery.current = queryKey;
        const cache = dbCache.current;
        if (cache.size >= 24) {
          const oldest = cache.keys().next().value;
          if (oldest !== undefined) cache.delete(oldest);
        }
        cache.set(cacheKey, { table, expires: Date.now() + 30_000 });
        setObjects(table);
        setObjectOffset(offset);
      }
    } catch (e) {
      if (generation === dbGeneration.current) throw e;
    } finally {
      if (generation === dbGeneration.current) {
        dbRequest.current = undefined;
        setDbBusy(false);
      }
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
  const matches = useMemo(() => flatCommands(commands).filter((n) =>
    [n.entry.caption, n.entry.sysId, n.entry.commandId, n.entry.id].some((v) =>
      v.toLowerCase().includes(menuQuery.toLowerCase()),
    ),
  ), [commands, menuQuery]);
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
  async function openController(link: string, employeeTab = false, sysId = "", openSource = false, preserveNavigation = false) {
    const root = workspace;
    const generation = controllerRequest.current;
    const selection = ++controllerSelection.current;
    const key = JSON.stringify([root, link, sysId]);
    const cache = controllerCache.current;
    let found = cache.get(key);
    if (!found) {
      setControllerRequests((count) => count + 1);
      try {
        found = await operation<{ file: string; files: string[] }>(
          "project.controller", { root, link, sysId }, 20_000,
        ).promise;
      } finally {
        setControllerRequests((count) => count - 1);
      }
    }
    if (generation !== controllerRequest.current || root !== latest.current.settings.workspace) return;
    if (cache.size >= 64) {
      const oldest = cache.keys().next().value;
      if (oldest !== undefined) cache.delete(oldest);
    }
    cache.set(key, found);
    const id = JSON.stringify([link, sysId]);
    const group = { id, link, ...found, employeeTab, sysId };
    setControllers((current) => current.some((entry) => entry.id === id)
      ? current.map((entry) => entry.id === id ? { ...group, employeeTab: employeeTab || entry.employeeTab } : entry) : [...current, group]);
    if (selection === controllerSelection.current) {
      setExpandedController(id);
      if (!preserveNavigation) {
        setNav("files");
        setSide(true);
      }
      if (openSource && found.file) await open(found.file);
    }
  }
  function openFunctionController(node: CommandNode) {
    setSelectedCommand(node.entry.id);
    if (selectedTool === "advance-note") {
      setAdvanceMenuPick({
        id: node.entry.id, caption: node.entry.caption, sysId: node.entry.sysId,
        link: node.entry.link, sequence: Date.now(),
      });
      void guard(() => openController(node.entry.link, false, node.entry.sysId, false, true));
      return;
    }
    void guard(() => openController(node.entry.link, false, node.entry.sysId));
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
  const closingProjectFiles = useRef(false);
  async function closeProjectFiles(owner?: Project) {
    if (closingProjectFiles.current) return;
    if (running) { setStatus("Dừng query đang chạy trước khi đóng nhóm file."); return; }
    closingProjectFiles.current = true;
    const closed = new Set<string>();
    try {
      const ownerIds = new Set((owner ? [owner] : projects).map(item => item.id));
      const closing = latest.current.tabs.filter(tab => {
        const projectId = tab.project ?? projects.filter(item => tab.path && item.sourcePath && pathIsWithin(tab.path, item.sourcePath))
          .sort((left, right) => right.sourcePath.length - left.sourcePath.length)[0]?.id;
        return !!projectId && ownerIds.has(projectId);
      });
      const edited = closing.filter(dirty);
      if (edited.length) {
        const choice = await askUnsaved(edited.map(tab => tab.name));
        if (choice === "cancel") return;
        if (choice === "save") for (const tab of edited) if (!(await save(tab))) return;
      }
      for (const tab of closing) {
        if (native) await request("document.close", { id: tab.id });
        closed.add(tab.id);
        disposeModel(tab);
        resultsByTab.current.delete(tab.id);
      }
      setStatus(closing.length ? "Đã đóng " + closing.length + " tab của " + (owner?.name ?? "tất cả dự án") : "Không có tab dự án đang mở.");
    } finally {
      if (closed.size) {
        const remaining = latest.current.tabs.filter(tab => !closed.has(tab.id));
        setTabs(current => current.filter(tab => !closed.has(tab.id)));
        setActiveIdRaw(current => closed.has(current) ? remaining.at(-1)?.id ?? "" : current);
        setPaneIds(current => current.map(id => closed.has(id) ? remaining.at(-1)?.id ?? "" : id));
      }
      closingProjectFiles.current = false;
    }
  }
  async function openHistoryFile(p: Project, path: string) {
    if (switchingHistoryProject.current) return;
    switchingHistoryProject.current = true;
    try {
      if (project && project.id !== p.id) {
        const oldTargetIds = projectTargetIds.current;
        const closing = latest.current.tabs.filter((tab) => tab.project === project.id ||
          (!tab.project && ((tab.path && project.sourcePath && pathIsWithin(tab.path, project.sourcePath)) ||
            (tab.target && oldTargetIds.includes(tab.target.id)))));
        const edited = closing.filter(dirty);
        if (edited.length) {
          const choice = await askUnsaved(edited.map((tab) => tab.name));
          if (choice === "cancel") return;
          if (choice === "save")
            for (const tab of edited) if (!(await save(tab))) return;
        }
        await openProject(p, oldTargetIds);
        await Promise.all(closing.map((tab) => request("document.close", { id: tab.id })));
        const closingIds = new Set(closing.map((tab) => tab.id));
        setTabs((current) => current.filter((tab) => !closingIds.has(tab.id)));
        setActiveIdRaw((current) => closingIds.has(current) ? "" : current);
        setPaneIds((current) => current.map((id) => closingIds.has(id) ? "" : id));
        closing.forEach(disposeModel);
      } else if (!project) {
        await openProject(p);
      }
      setSelectedTool(undefined);
      await open(path, undefined, undefined, p.id);
    } finally {
      switchingHistoryProject.current = false;
    }
  }
  async function openProject(p: Project, closingTargets: string[] = []) {
    const opened = await operation<{
      project: Project;
      targets: Target[];
    }>("project.open", { id: p.id }, 31_000).promise;
    setProject(opened.project);
    setActiveId("");
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
    setTargets((old) => [...old.filter((target) => !closingTargets.includes(target.id)), ...opened.targets]);
    setCommands([]);
    const sys =
      opened.targets.find((t) => /_S(?:ys)?$/i.test(t.database)) ??
      opened.targets[1];
    projectSysTarget.current = sys;
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
    controllerRequest.current++;
    setControllers([]);
    setExpandedController(undefined);
    setDialog("");
  }
  function runProjectAction(work: () => Promise<void>) {
    setProjectActionBusy(true);
    void guard(async () => {
      try { await work(); }
      finally { setProjectActionBusy(false); }
    });
  }
  async function renameWorkspaceItem(path: string, name: string) {
    await Promise.all([...pendingOpens.current.values()]);
    const destination = await request<string>("workspace.rename", { path, name });
    const moved = (value: string) => rebasePath(value, path, destination);
    setTabs((current) => current.map((tab) => {
      if (!tab.path) return tab;
      const nextPath = moved(tab.path);
      return nextPath === tab.path ? tab : {
        ...tab, path: nextPath, name: nextPath.split(/[\\/]/).at(-1) ?? tab.name,
      };
    }));
    setControllers((current) => current.map((entry) => ({
      ...entry, file: moved(entry.file), files: entry.files.map(moved),
    })));
    setHits((current) => current.map((hit) => ({ ...hit, path: moved(hit.path) })));
    setExternal((current) => current?.path ? { ...current, path: moved(current.path) } : current);
    controllerCache.current.clear();
    setStatus("Đã đổi tên thành " + name);
    return destination;
  }
  function copyWorkspaceText(text: string) {
    void guard(async () => {
      await writeClipboard(text);
      setStatus("Đã sao chép " + text);
    });
  }
  function revealWorkspacePath(path: string) {
    void guard(() => request("workspace.reveal", { path }));
  }
  async function duplicateWorkspaceItem(path: string) {
    const copied = await request<string>("workspace.duplicate", { path });
    setControllers((current) => current.map((entry) => entry.files.includes(path)
      ? { ...entry, files: [...entry.files, copied] } : entry));
    controllerCache.current.clear();
    await open(copied);
    setStatus("Đã nhân bản " + copied);
  }
  return (
    <div className="studio" data-ready={ready}>
      <header className="topbar">
        <div className="brand">
          <img className="brand-mark app-logo" src={logo} alt="" />
          <strong>Studio Code</strong>
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
          <Button
            variant="ghost"
            className="program-button"
            onClick={() =>
              void guard(async () => {
                if (!project) throw new Error("Chọn dự án trước.");
                await request("project.program", { id: project.id });
              })
            }
          >
            <ArrowUpRight size={17} />
            Mở chương trình
          </Button>
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
      <div className="contextbar loading-region" aria-busy={openingInitialProject}>
        <LoadingLine busy={openingInitialProject} label="Đang mở dự án…" />
        <IconButton label="Làm mới web.config" disabled={!project || projectActionBusy} onClick={() => {
          if (!project) return;
          runProjectAction(async () => {
            await request("project.refreshConfig", { id: project.id });
            setStatus("Đã lưu lại web.config");
          });
        }}><RefreshCw size={15} /></IconButton>
        <IconButton label="Xóa Structure/App" disabled={!project || projectActionBusy} onClick={() => {
          if (!project) return;
          runProjectAction(async () => {
            const preview = await request<{ path: string; count: number }>("project.structure.preview", { id: project.id });
            if (preview.count) setStructureClear({ id: project.id, ...preview });
            else setStatus("Structure/App không có file để xóa");
          });
        }}><Trash2 size={15} /></IconButton>
        <Button variant="ghost" onClick={() => setDialog("projects")}>
          <FolderOpen size={16} />
          <span className="context-name">{project?.name ?? "Chọn dự án"}</span>
          <ChevronDown size={13} />
        </Button>
        <span className="context-divider" />
        {workspace ? (
          <button
            type="button"
            className="workspace-path workspace-path-action"
            title={`${workspace}\nNhấp để mở thư mục · Chuột phải để sao chép`}
            onClick={() => void guard(() => request("workspace.reveal", { path: workspace }))}
            onContextMenu={(event) => {
              event.preventDefault();
              void guard(async () => {
                await writeClipboard(workspace);
                setStatus("Đã sao chép đường dẫn thư mục");
              });
            }}
          >
            {workspace}
          </button>
        ) : <span className="workspace-path">Chưa mở thư mục</span>}
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
      <div className={`workbench ${side ? "" : "side-closed"}`} style={{
        "--workspace-width": (settings.explorerWidth ?? 264) + "px",
        "--explorer-font-size": (settings.explorerFontSize ?? 12) + "px",
      } as CSSProperties}>
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
              "advance-note",
              "profiler",
              "template-studio",
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
                    "advance-note": NotebookPen,
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
          <aside className="sidebar" id="project-sidebar">
            <div className="sidebar-heading">
              <h2>
                {
                  {
                    files: "Không gian dự án",
                    functions: "Chức năng ERP",
                    database: "Database",
                    search: "Tìm kiếm",
                    history: "Lịch sử file",
                  }[nav]
                }
              </h2>
              <div className="sidebar-heading-actions">
              <IconButton
                label="Tải lại"
                onClick={() =>
                  void guard(async () => {
                    controllerCache.current.clear();
                    if (nav === "files" && controller) await openController(controller.link, controller.employeeTab, controller.sysId);
                    if (nav === "functions") await loadCommands();
                    if (nav === "database") await loadObjects(0, true);
                  })
                }
              >
                <RefreshCw size={14} />
              </IconButton>
              <IconButton label="Ẩn không gian dự án" onClick={() => setSide(false)}>
                <PanelLeftClose size={15} />
              </IconButton>
              </div>
            </div>
            <div className="side-tabs">
              {[
                ["files", "File"],
                ["functions", "Chức năng"],
                ["database", "DB"],
                ["history", "Lịch sử"],
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
            {nav === "history" && (
              <>
              <div className="side-form">
                <input type="search" aria-label="Tìm file hoặc thư mục trong lịch sử"
                  placeholder="Tìm tên file hoặc thư mục" value={historySearch}
                  onChange={(event) => setHistorySearch(event.target.value)} />
              </div>
              <div className="side-content project-file-tree">
                {historyResults?.length ? historyResults.map(({ projectId, files }) => {
                  const owner = projects.find((entry) => entry.id === projectId);
                  return owner && <ProjectHistory key={projectId} name={owner.name}
                    root={owner.sourcePath} files={files} activePath={active?.path}
                    onCloseProject={() => void guard(() => closeProjectFiles(owner))}
                    onCloseAllProjects={() => void guard(() => closeProjectFiles())}
                    onOpen={(path) => void guard(() => openHistoryFile(owner, path))}
                    onRemove={(path) => void guard(async () => {
                      await request("project.history.remove", { id: projectId, path });
                      setHistoryRevision((version) => version + 1);
                    })} />;
                }) : <div className="explorer-message">{!history ? "Đang tải lịch sử…" : history.length && historySearch.trim()
                  ? "Không tìm thấy file hoặc thư mục." : "Chưa có file trong lịch sử."}</div>}
              </div>
              </>
            )}
            {nav === "files" && (
              <>
              <div className="side-content side-wide">
                {workspace ? (
                  <>
                    <div className="section-label">
                      <FolderOpen size={14} />
                      <span>{workspace.split(/[\\/]/).at(-1)}</span>
                    </div>
                    {!controllers.length && <div className="explorer-message">Mở một chức năng ERP để xem file của controller.</div>}
                    {controllers.map((group) => {
                      const expanded = expandedController === group.id;
                      const label = (group.file || group.link).split(/[\\/]/).at(-1)?.replace(/\.[^.]+$/, "") ?? group.link;
                      return <div className="controller-group" key={group.id}>
                        <div className="controller-group-header">
                        <button className="tree-row folder-row controller-group-title" aria-expanded={expanded} title={group.link}
                          onClick={() => { controllerSelection.current++; setExpandedController(expanded ? undefined : group.id); }}>
                          {expanded ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
                          <Folder size={15} className="folder-icon" /><span>{label}</span>
                        </button>
                        <IconButton label={"Đóng controller " + label} onClick={() => {
                          setControllers((current) => current.filter((entry) => entry.id !== group.id));
                          if (expanded) setExpandedController(undefined);
                        }}><X size={13} /></IconButton>
                        </div>
                        {expanded && <div className="tree-children">
                          <ControllerExplorer
                            root={workspace}
                            files={group.files}
                            emptyMessage="Controller chưa có file."
                            activePath={active?.path}
                            onOpen={(p) => void guard(() => open(p))}
                            onCopy={copyWorkspaceText}
                            onReveal={revealWorkspacePath}
                            onDuplicate={(path) => void guard(() => duplicateWorkspaceItem(path))}
                            onRename={renameWorkspaceItem}
                            onSourceCollection={samples => setSourceCollection({ samples, destination: workspace })}
                          />
                        </div>}
                      </div>;
                    })}
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
              {controller && (controller.employeeTab || isEmployeeController(controller)) && (
                <EmployeeFunctionTabs
                  key={JSON.stringify([workspace, projectSysTarget.current?.id])}
                  targetId={projectSysTarget.current?.id}
                  selected={isEmployeeController(controller) ? employeeController : (controller.sysId || controller.link).toLowerCase()}
                  onOpen={(sysId) => void guard(() => {
                    const general = sysId.toLowerCase() === employeeController
                      ? controllers.find(isEmployeeController) : undefined;
                    return openController(general?.link ?? sysId, true, sysId);
                  })}
                />
              )}
              </>
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
                  <Select label="Phạm vi tìm kiếm" value={searchRoot === workspace ? "" : searchRoot}
                    options={[{ value: "", label: "Toàn dự án" }, ...searchFolders.map((folder) => ({
                      value: folder, label: folder.slice(workspace.length).replace(/^[\\/]+/, "") || folder,
                    }))]}
                    onChange={(folder) => {
                      const previous = searchRef.current;
                      searchRef.current = undefined;
                      void previous?.cancel().catch(() => {});
                      setSearching(false);
                      setHits([]);
                      setSearchFolder(folder);
                    }} />
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
                      if (e.key === "Enter") void guard(() => loadObjects(0, true));
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
                      onClick={() => void guard(() => loadObjects(0, true))}
                    >
                      Tìm
                    </Button>
                  </div>
                </div>
                <div className="side-content loading-region" aria-busy={dbBusy}>
                  <LoadingLine busy={dbBusy} label="Đang tải database…" />
                  {objects?.rows.map((r, i) => (
                      <button
                        className="db-row"
                        key={i}
                        onClick={() => {
                          if (!tableTab || String(r[2] ?? "").toUpperCase().replaceAll("_", " ") !== "USER TABLE") return;
                          const schema = typeof r[0] === "string" && r[0].trim() ? r[0].trim() : "dbo";
                          const table = typeof r[1] === "string" ? r[1].trim() : "";
                          if (!table) return;
                          const selected = quoteSqlName(schema, table);
                          setTableTabs((current) => current.map((entry) =>
                            entry.id === tableTab.id ? { ...entry, table: selected } : entry));
                          setSelectedTool(tableTab.id);
                        }}
                        onDoubleClick={() =>
                          void guard(async () => {
                            const script = await request<string>("schema.script", {
                              targetId: target?.id,
                              object: quoteSqlName(r[0] ?? "", r[1] ?? ""),
                            });
                            scriptToFocus.current = newDocument(script, r[1] + ".sql", target).id;
                          })
                        }
                      >
                        <Database size={14} />
                        <span>
                          {r[0]}.{r[1]}
                          <small>{r[2]?.replaceAll("_", " ")}</small>
                        </span>
                        <ArrowUpRight size={12} />
                      </button>
                    )) ?? (!dbBusy && (
                      <div className="empty">
                        Nhấn Tìm để tải danh sách object.
                      </div>
                    ))}
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
                    <span>{objects.rows.length ? `${objectOffset + 1}–${objectOffset + objects.rows.length}` : "0 kết quả"}</span>
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

                </div>
                <div className="side-content side-wide loading-region" aria-busy={controllerRequests > 0}>
                  <LoadingLine busy={controllerRequests > 0} label="Đang tải cây controller…" />
                  {!menuQuery && (
                    <FunctionTree
                      nodes={commands}
                      selected={selectedCommand}
                      onSelect={setSelectedCommand}
                      onOpen={openFunctionController}
                      onOpenSource={(node) => void guard(() => openController(node.entry.link, false, node.entry.sysId, true))}
                      onEdit={openMenuEditor}
                    />
                  )}
                  {(menuQuery ? matches : []).map((n, i) => (
                    <div className="function-row" key={n.entry.id}>
                      <button
                        className="search-hit"
                        aria-current={
                          selectedCommand === n.entry.id || (menuQuery && i === menuIndex) ? "page" : undefined
                        }
                        onClick={() => setSelectedCommand(n.entry.id)}
                        onDoubleClick={() => openFunctionController(n)}
                        onFocus={() => setSelectedCommand(n.entry.id)}
                        onKeyDown={(event) => {
                          if (event.key === "F3") {
                            event.preventDefault();
                            openMenuEditor(n);
                          }
                        }}
                        onContextMenu={(event) => {
                          event.preventDefault();
                          setSelectedCommand(n.entry.id);
                          openMenuEditor(n);
                        }}
                      >
                        <strong>{n.entry.caption}</strong>
                      </button>
                      {!n.children.length && (n.entry.link || n.entry.sysId) ? <button
                        type="button"
                        className="function-menu-id"
                        title="Mở source tại File"
                        onClick={(event) => {
                          event.stopPropagation();
                          setSelectedCommand(n.entry.id);
                          void guard(() => openController(n.entry.link, false, n.entry.sysId, true));
                        }}
                      >{n.entry.id}</button> : <small className="function-menu-id">{n.entry.id}</small>}
                    </div>
                  ))}
                  {commands.length === 0 && (
                    <div className="empty">Tải chức năng từ SYS của dự án.</div>
                  )}
                </div>
              </>
            )}
            <WorkspaceResizeHandle width={settings.explorerWidth ?? 264} onCommit={(explorerWidth) => {
              const next = { ...latest.current.settings, explorerWidth };
              latest.current.settings = next;
              setSettings(next);
              if (native) void guard(() => request("settings.save", { settings: next }));
            }} />
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
                className={`document-tab ${!selectedTool && t.id === activeId ? "active" : ""}`}
                key={t.id}
              >
                <button
                  role="tab"
                  aria-selected={!selectedTool && t.id === activeId}
                  aria-label={tabDetails.get(t.id)?.label}
                  title={t.path ?? t.name}
                  onClick={() => setActiveId(t.id)}
                >
                  <FileIcon path={t.name} />
                  <span>{t.name}</span>
                  {tabDetails.get(t.id)?.folder && <small className="tab-folder">{tabDetails.get(t.id)?.folder}</small>}
                  {tabDetails.get(t.id)?.changed && (
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
            {(watchTab || menuEditor || profilerTab || studioOpen || advanceNoteOpen || tableTabs.length > 0) && (
              <div className="workspace-tools" role="tablist" aria-label="Công cụ làm việc">
                {watchTab && <div className={`document-tab ${selectedTool === "sql-watch" ? "active" : ""}`}>
                  <button id="sql-watch-tab" role="tab" aria-controls="sql-watch-panel" aria-selected={selectedTool === "sql-watch"} onClick={() => setSelectedTool("sql-watch")}>SQL Debug / Watch</button>
                  <IconButton label="Đóng SQL Watch" onClick={closeWatchTab}><X size={13} /></IconButton>
                </div>}
                {menuEditor && <div className={`document-tab ${selectedTool === "menu-editor" ? "active" : ""}`}>
                  <button id="menu-editor-tab" role="tab" aria-controls="menu-editor-panel" aria-selected={selectedTool === "menu-editor"}
                    onClick={() => setSelectedTool("menu-editor")} title={menuEditor.command.entry.caption}>
                    <span>Menu ERP · {menuEditor.command.entry.id}</span>
                  </button>
                  <IconButton label="Đóng thông tin menu ERP" onClick={closeMenuEditor}><X size={13} /></IconButton>
                </div>}
                {studioOpen && <div className={`document-tab ${selectedTool === "template-studio" ? "active" : ""}`}>
                  <button id="template-studio-tab" role="tab" aria-controls="template-studio-panel"
                    aria-selected={selectedTool === "template-studio"} onClick={() => setSelectedTool("template-studio")}>
                    <span>Dynamic Template Studio</span>
                  </button>
                  <IconButton label="Đóng Dynamic Template Studio" onClick={closeStudio}><X size={13} /></IconButton>
                </div>}
                {advanceNoteOpen && <div className={`document-tab ${selectedTool === "advance-note" ? "active" : ""}`}>
                  <button id="advance-note-tab" role="tab" aria-controls="advance-note-panel"
                    aria-selected={selectedTool === "advance-note"} onClick={() => setSelectedTool("advance-note")}>
                    <NotebookPen size={15} /><span>Generate Update</span>
                  </button>
                  <IconButton label="Đóng Generate Update" onClick={closeAdvanceNote}><X size={13} /></IconButton>
                </div>}
                {profilerTab && (
                  <div className={`document-tab ${profilerSelected ? "active" : ""}`}>
                    <button id="profiler-tab" role="tab" aria-controls="profiler-panel" aria-selected={profilerSelected} onClick={() => setSelectedTool("profiler")}>
                      <Activity size={15} /><span>SQL Profiler</span>
                    </button>
                    <IconButton label="Đóng SQL Profiler" disabled={profilerBusy || closingProfiler} onClick={() => void guard(closeProfilerTab)}><X size={13} /></IconButton>
                  </div>
                )}
                {tableTabs.map((tab) => {
                  const label = "Cấu trúc và dữ liệu" + (tab.number > 1 ? " " + tab.number : "") + (tab.table ? " · " + tab.table : "");
                  return <div className={`document-tab ${tab.id === selectedTool ? "active" : ""}`} key={tab.id}>
                    <button id={tab.id + "-tab"} role="tab" aria-controls={tab.id + "-panel"} aria-selected={tab.id === selectedTool}
                      title={label} aria-label={label} onClick={() => setSelectedTool(tab.id)}>
                      <Database size={15} /><span>{tab.table ?? label}</span>
                    </button>
                    <IconButton label={"Đóng " + label} onClick={() => closeTableTab(tab.id)}><X size={13} /></IconButton>
                  </div>;
                })}
              </div>
            )}
            <div className="toolbar-spacer" />
            {tableTab && <div id="table-toolbar-slot" className="table-toolbar-slot" aria-label="Công cụ bảng" />}
            {active && !selectedTool && (
              <>
                <div className="editor-search">
                  <Search size={14} aria-hidden="true" />
                  <input
                    type="search"
                    aria-label="Tìm trong file đang mở"
                    placeholder="Tìm trong file…"
                    value={editorSearchQuery}
                    onChange={(event) => {
                      const query = event.target.value;
                      setEditorSearchQuery(query);
                      if (query.length > 0) {
                        setBottom("results");
                        setPanel(true);
                      }
                    }}
                  />
                </div>
                {language(active.name) === "xml" && (
                  <Button variant="ghost" onClick={() => setDialog("editor.controllerPreview")}>Xem trước Grid / Dir</Button>
                )}
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
                  className="sql-run-button"
                  aria-label="Chạy SQL"
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
                <div className="format-menu" ref={formatMenuRef}>
                  <IconButton
                    label="Định dạng"
                    aria-expanded={formatMenuOpen}
                    aria-haspopup="menu"
                    onClick={() => setFormatMenuOpen((open) => !open)}
                  >
                    <Braces size={16} />
                  </IconButton>
                  {formatMenuOpen && (
                    <div className="format-menu-options" role="menu" aria-label="Định dạng">
                      {([
                        ["sql", "Format SQL"],
                        ["json", "Format Json"],
                        ["xml", "Format XML"],
                      ] as const).map(([kind, label]) => (
                        <button
                          key={kind}
                          type="button"
                          role="menuitem"
                          onClick={() => {
                            setFormatMenuOpen(false);
                            void guard(() => format(kind));
                          }}
                        >
                          {label}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </>
            )}
          </div>
          {watchTab && <section id="sql-watch-panel" className="profiler-panel" role="tabpanel" aria-labelledby="sql-watch-tab" hidden={selectedTool !== "sql-watch"}>
            <SqlWatchTool targets={targets} initialTarget={watchTab.target}
              onFromEditor={() => active ? textOf(active) : ""} onBusyChange={value => { watchBusy.current = value; }} />
          </section>}
          {menuEditor && <section id="menu-editor-panel" className="profiler-panel" role="tabpanel"
            aria-labelledby="menu-editor-tab" hidden={selectedTool !== "menu-editor"}>
            <MenuEditor key={menuEditor.target.id + ":" + menuEditor.command.entry.id}
              target={menuEditor.target} command={menuEditor.command} root={menuEditor.root}
              onClose={closeMenuEditor} onDirtyChange={value => { menuDirty.current = value; }}
              onBusyChange={value => { menuBusy.current = value; }}
              onOpenPath={path => void guard(() => open(path))}
               onCopy={copyWorkspaceText}
               onReveal={revealWorkspacePath}
              onSaved={async id => {
                if (projectSysTarget.current?.id === menuEditor.target.id) {
                  await loadCommands(); setSelectedCommand(id);
                }
                setStatus("Đã lưu thông tin menu " + id);
              }} />
          </section>}
          {studioOpen && <section id="template-studio-panel" className="profiler-panel" role="tabpanel"
            aria-labelledby="template-studio-tab" hidden={selectedTool !== "template-studio"}>
            <TemplateStudio onDirty={(dirty) => { studioDirty.current = dirty; }}
              onBusy={(busy) => { studioBusy.current = busy; }} />
          </section>}
          {advanceNoteOpen && project && <section id="advance-note-panel" className="profiler-panel" role="tabpanel"
            aria-labelledby="advance-note-tab" hidden={selectedTool !== "advance-note"}>
            <AdvanceNote projectId={project.id} projectName={project.name} root={workspace}
              databaseTargets={{ App: defaultTarget.current?.id, Sys: projectSysTarget.current?.id }}
              dataCapture={advanceDataCapture}
              activeMenu={advanceMenuPick}
              source={controller ? { link: controller.link, sysId: controller.sysId, files: controller.files } : undefined}
              onLoadMenu={(menu: AdvanceNoteMenu) => void guard(() => openController(menu.link, false, menu.sysId, false, true))}
              onCopy={copyWorkspaceText} onReveal={revealWorkspacePath}
              onDirtyChange={(dirty) => { advanceNoteDirty.current = dirty; }} onStatus={setStatus} />
          </section>}
          {profilerTab && (
            <section id="profiler-panel" className="profiler-panel" role="tabpanel" aria-labelledby="profiler-tab" hidden={!profilerSelected}>
              <ProfilerTool target={profilerTab.target ?? target} visible={profilerSelected} closing={closingProfiler} onBusyChange={setProfilerBusy} />
            </section>
          )}
          {tableTabs.map((tab) => (
            <section key={tab.id} id={tab.id + "-panel"} className="profiler-panel" role="tabpanel" aria-labelledby={tab.id + "-tab"} hidden={tab.id !== selectedTool}>
              <TableToolErrorBoundary key={tab.id + ":" + (tab.table ?? "")}>
                <TableTool scope={tab.id} target={tab.target} initialTable={tab.table} toolbarSlotId={tab.id === selectedTool ? "table-toolbar-slot" : undefined} visible={tab.id === selectedTool}
                  targets={project ? targets.filter((entry) => projectTargetIds.current.includes(entry.id) || entry.id === tab.target?.id) : targets}
                  onTargetChange={(target) => setTableTabs((current) => current.map((entry) =>
                    entry.id === tab.id ? { ...entry, target, table: undefined } : entry))}
                  onTableChange={(table) => {
                    if (tab.table !== table)
                      setTableTabs((current) => current.map((entry) => entry.id === tab.id ? { ...entry, table } : entry));
                  }}
                  onText={(text, name) => newDocument(text, name, tab.target)}
                  onCaptureData={(capturedTarget, table, condition) => {
                    const database = projectSysTarget.current?.id === capturedTarget.id ? "Sys" : "App";
                    setAdvanceNoteOpen(true);
                    setAdvanceDataCapture({ sequence: ++advanceDataSequence.current, database, table, where: condition });
                  }}
                  onConnect={() => setDialog("connection")} />
              </TableToolErrorBoundary>
            </section>
          ))}
          <div className="editor-content" hidden={!!selectedTool}>
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
                            options={paneOptions}
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
                        fontSize={settings.editorFontSize}
                        target={doc.target}
                        onMount={(e) => {
                          if (pane === 0) primaryEditor.current = e;
                          if (scriptToFocus.current === doc.id && paneRef.current === pane) {
                            scriptToFocus.current = undefined;
                            const lineNumber = e.getModel()?.getLineCount() ?? 1;
                            e.setPosition({ lineNumber, column: 1 });
                            e.revealLine(lineNumber);
                            e.focus();
                          }
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
          {panel && <PanelResizeHandle />}
          <section id="results-panel" className={`bottom-panel ${panel ? "" : "collapsed"}`}>
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
                    {id === "results" && editorSearchQuery.length > 0 ? (
                      <span className="count">{editorSearch.truncated ? "200+" : editorSearch.hits.length}</span>
                    ) : id === "results" && result ? (
                      <span className="count">{result.tables.length}</span>
                    ) : null}
                  </button>
                ))}
              </div>
              <div className="toolbar-spacer" />
              {panel && !editorSearchQuery && result?.tables[resultIndex] && bottom === "results" && (
                <Button variant="ghost" onClick={exportCsv}>
                  Xuất CSV
                </Button>
              )}
              <IconButton
                label={panel ? "Ẩn panel" : "Hiện panel"}
                onClick={() => setPanel(!panel)}
              >
                <PanelBottom size={15} />
              </IconButton>
            </div>
            {panel && !editorSearchQuery && bottom === "results" && !running && result && result.tables.length > 1 && (
              <div className="result-table-picker" role="group" aria-label="Bảng kết quả">
                {result.tables.map((table, index) => (
                  <Button
                    key={index}
                    variant="ghost"
                    aria-pressed={resultIndex === index}
                    onClick={() => setResultIndex(index)}
                  >
                    Kết quả {index + 1} · {table.rows.length} dòng
                  </Button>
                ))}
              </div>
            )}
            {panel &&
              (bottom === "results" ? (
                editorSearchQuery.length > 0 ? (
                  <div className="editor-search-panel" role="listbox" aria-label="Kết quả tìm trong file">
                    {editorSearch.hits.length === 0 ? (
                      <div className="editor-search-empty">Không tìm thấy dòng nào.</div>
                    ) : (
                      <>
                        {editorSearch.hits.map((hit) => (
                          <button
                            type="button"
                            role="option"
                            key={`${hit.line}:${hit.column}`}
                            onClick={() => focusEditorSearchHit(hit)}
                          >
                            <span className="editor-search-line">{hit.line}</span>
                            <span className="editor-search-preview">{hit.preview || " "}</span>
                          </button>
                        ))}
                        {editorSearch.truncated && (
                          <div className="editor-search-empty">Đang hiển thị 200 dòng đầu tiên.</div>
                        )}
                      </>
                    )}
                  </div>
                ) : running ? (
                  <div className="empty">
                    <span className="loading-dot" />
                    Đang thực thi query…
                  </div>
                ) : result?.tables[resultIndex] ? (
                  <DataGrid key={activeId + ":" + resultIndex} table={result.tables[resultIndex]} rangeSelection />
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
                          {d.severity === "warning" ? "Cảnh báo · " : ""}{d.message}
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
          </div>
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
            if (!project && !tableSelected) defaultTarget.current = t;
            setTargets((old) => [...old, t]);
            if (tableTab) setTableTabs((current) => current.map((entry) => entry.id === tableTab.id ? { ...entry, target: t, table: undefined } : entry));
            else setTabs((old) =>
              old.map((d) => (d.id === activeId ? { ...d, target: t } : d)),
            );
            if (!active && !tableSelected)
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
      {sourceCollection && <SourceCollectionDialog {...sourceCollection} onClose={() => setSourceCollection(undefined)} />}
      {dialog === "projects" && (
        <ProjectDialog
          projects={projects}
          lastAccess={settings.projectLastAccess}
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
      {structureClear && (
        <Modal title="Xóa file trong Structure/App" onClose={() => {
          if (!projectActionBusy) setStructureClear(undefined);
        }}>
          <div className="tool-body">
            <p>Xóa {structureClear.count} file trong thư mục sau và các thư mục con?</p>
            <p className="break-all">{structureClear.path}</p>
            <div className="inline-checks">
              <Button disabled={projectActionBusy} onClick={() => setStructureClear(undefined)}>Hủy</Button>
              <Button disabled={projectActionBusy} onClick={() => runProjectAction(async () => {
                const count = await request<number>("project.structure.clear", { id: structureClear.id, path: structureClear.path });
                controllerCache.current.clear();
                setControllers((current) => current.map((entry) => ({
                  ...entry, files: entry.files.filter((file) => !pathIsWithin(file, structureClear.path)),
                })));
                setStructureClear(undefined);
                setHistoryRevision((version) => version + 1);
                setStatus("Đã xóa " + count + " file trong Structure/App");
              })}>{projectActionBusy ? "Đang xóa…" : "Xóa file"}</Button>
            </div>
          </div>
        </Modal>
      )}
      {dialog === "editor.controllerPreview" && active && (
        <ControllerPreview key={active.id} text={textOf(active)} path={active.path} onClose={() => setDialog("")} />
      )}
      {dialog === "editor.sections" && active && (
        <FileSections name={active.name} text={textOf(active)} onClose={() => setDialog("")}
          onJump={(offset) => {
            const position = modelFor(active).getPositionAt(offset);
            setDialog("");
            requestAnimationFrame(() => {
              editor.current?.setPosition(position);
              editor.current?.revealLineInCenter(position.lineNumber);
              editor.current?.focus();
            });
          }} />
      )}
      {dialog && !["connection", "projects", "settings", "editor.sections", "editor.controllerPreview"].includes(dialog) && (
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
              fontSize={settings.editorFontSize}
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
  onOpenSource,
  onEdit,
}: {
  nodes: CommandNode[];
  selected: string;
  onSelect: (id: string) => void;
  onOpen: (node: CommandNode) => void;
  onOpenSource: (node: CommandNode) => void;
  onEdit: (node: CommandNode) => void;
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
            <div className="function-row">
              <button
                className="tree-row"
                 aria-label={node.entry.caption}
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
                onKeyDown={(event) => {
                  if (event.key === "F3") {
                    event.preventDefault();
                    onEdit(node);
                  }
                }}
                onContextMenu={(event) => {
                  event.preventDefault();
                  onSelect(node.entry.id);
                  onEdit(node);
                }}
                onDoubleClick={() => {
                  if (node.entry.link || node.entry.sysId) onOpen(node);
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
                <span className="min-w-0 truncate">{node.entry.caption}</span>
              </button>
              {!node.children.length && (node.entry.link || node.entry.sysId) ? <button
                type="button"
                className="function-menu-id"
                title="Mở source tại File"
                onClick={(event) => {
                  event.stopPropagation();
                  onSelect(node.entry.id);
                  onOpenSource(node);
                }}
              >{node.entry.id}</button> : <small className="function-menu-id">{node.entry.id}</small>}
            </div>
            {node.children.length > 0 && expanded.has(node.entry.id) && (
              <div className="tree-children">
                <FunctionTree
                  nodes={node.children}
                  selected={selected}
                  onSelect={onSelect}
                  onOpen={onOpen}
                  onOpenSource={onOpenSource}
                  onEdit={onEdit}
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
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const frame = requestAnimationFrame(() => inputRef.current?.focus({ preventScroll: true }));
    return () => cancelAnimationFrame(frame);
  }, [kind]);
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
        ref={inputRef}
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
