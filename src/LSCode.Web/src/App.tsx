import { lazy, Suspense } from "react";
const CheckMail = lazy(() => import("./features/CheckMail"));
import ControllerExplorer, { FileIcon } from "./ControllerExplorer";
import { SourceCollectionDialog } from "./features/SourceCollectionDialog";
import { pathIsWithin, rebasePath, tabFolderLabel } from "./fileLabels";
import { FileSections } from "./features/FileSections";
import { CommandBox } from "./features/CommandBox";
import { RequestTracking } from "./features/RequestTracking";
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
  Copy,
  AlertTriangle,
} from "lucide-react";
import { native, request, operation, subscribe, quoteSqlName, notifyWindowReady, writeClipboard } from "./bridge";
import { PanelResizeHandle, WorkspaceResizeHandle } from "./PanelResizeHandle";
import { ProfilerTool } from "./features/ProfilerTool";
import { invalidateTableCatalog, TableTool, TableToolErrorBoundary } from "./features/TableTool";
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
  syncSavedText,
  disposeModel,
  language,
  markSqlErrors,
  setEditorSnippets,
  xmlIncludeBuffers,
} from "./editor";
import type { EditorDiagnostic, EditorHandle } from "./editor";
import { Button, IconButton, Select, Modal, DataGrid, WorkbenchNotice } from "./ui";
import { applyButtonTheme } from "./buttonTheme";
import {
  ToolDialog,
  ProjectDialog,
  ConnectionDialog,
  SettingsDialog,
} from "./tools";

type Tab = DocumentInfo & { saved: string; target?: Target; project?: string };
type TableTab = { id: string; number: number; target?: Target; table?: string };
type ControllerProblem = EditorDiagnostic & { path: string };
type ControllerGroup = {
  id: string;
  link: string;
  file: string;
  files: string[];
  problems: ControllerProblem[];
  diagnosticsLoading?: boolean;
  employeeTab?: boolean;
  sysId?: string;
};
const diagnosticPathKey = (path: string) => path.replaceAll("\\", "/").toLowerCase();
function controllerProblemsByPath(problems: ControllerProblem[]) {
  const result = new Map<string, EditorDiagnostic[]>();
  for (const problem of problems) {
    const current = result.get(problem.path) ?? [];
    result.set(problem.path, [...current, problem]);
  }
  return result;
}
const employeeController = "hrpigeneralinformation";
function isEmployeeController(group: ControllerGroup) {
  return group.files.some((path) => /(?:^|[\\/])hrpigeneralinformation\.(xml|f)$/i.test(path));
}
type Entry = { path: string; name: string; directory: boolean };
type Hit = { path: string; line: number; column: number; preview: string };
type IncludeLocation = {
  path: string;
  line: number;
  column: number;
  name: string;
  start: number;
  length: number;
};
type EntityPeek = {
  id: string;
  parentId?: string;
  level: number;
  source: DocumentInfo;
  snippet: DocumentInfo;
  location: IncludeLocation;
  contextPath: string;
  contextText: string;
  fullFile: boolean;
  compactHeight: number;
  anchor: {
    left: number;
    top: number;
    width: number;
    height: number;
    caretLeft: number;
    caretSide: "top" | "bottom";
  };
};

type SearchFolder = { folders: Map<string, SearchFolder>; files: Hit[] };

function SearchResultTree({ hits, root, onOpen }: { hits: Hit[]; root: string; onOpen: (hit: Hit) => void }) {
  const tree = useMemo(() => {
    const tree: SearchFolder = { folders: new Map(), files: [] };
    for (const hit of hits) {
      const relative = pathIsWithin(hit.path, root)
        ? hit.path.slice(root.replace(/[\\/]+$/, "").length + 1) : hit.path;
      let folder = tree;
      for (const name of relative.split(/[\\/]/).slice(0, -1)) {
        let child = folder.folders.get(name);
        if (!child) {
          child = { folders: new Map(), files: [] };
          folder.folders.set(name, child);
        }
        folder = child;
      }
      folder.files.push(hit);
    }
    return tree;
  }, [hits, root]);
  function render(folder: SearchFolder): React.ReactNode {
    return <>
      {[...folder.folders].map(([name, child]) => (
        <details key={name} open>
          <summary className="tree-row folder-row search-tree-folder"><Folder size={15} aria-hidden="true" /><span>{name}</span></summary>
          <div className="tree-children">{render(child)}</div>
        </details>
      ))}
      {folder.files.map(hit => (
        <button key={hit.path} className="search-hit" title={hit.path} onClick={() => onOpen(hit)}>
          <strong className="search-tree-filename"><FileIcon path={hit.path} />{hit.path.split(/[\\/]/).at(-1)}{hit.line > 0 ? `:${hit.line}` : ""}</strong>
          {hit.preview && <span>{hit.preview}</span>}
        </button>
      ))}
    </>;
  }
  return <div className="project-file-tree">{render(tree)}</div>;
}

type EditorSearchHit = {
  line: number;
  column: number;
  endColumn: number;
  preview: string;
};
type FormatLanguage = "sql" | "json" | "xml";
function formatQueryDuration(milliseconds: number) {
  if (milliseconds < 1000) return `${Math.max(0, milliseconds).toFixed(0)} ms`;
  if (milliseconds < 60_000) return `${(milliseconds / 1000).toFixed(milliseconds < 10_000 ? 1 : 0)} giây`;
  const minutes = Math.floor(milliseconds / 60_000);
  const seconds = ((milliseconds % 60_000) / 1000).toFixed(1).padStart(4, "0");
  return `${minutes}:${seconds}`;
}
function entityPeekAnchor(current?: EditorHandle): EntityPeek["anchor"] {
  const host = document.querySelector<HTMLElement>(".editors");
  const dom = current?.getDomNode();
  const position = current?.getPosition();
  const visible = position ? current?.getScrolledVisiblePosition(position) : null;
  if (!host || !dom || !visible)
    return { left: 16, top: 24, width: 760, height: 280, caretLeft: 28, caretSide: "top" };
  const hostRect = host.getBoundingClientRect();
  const editorRect = dom.getBoundingClientRect();
  const width = Math.max(300, Math.min(820, hostRect.width - 24));
  const height = Math.max(160, Math.min(360, Math.floor(hostRect.height * 0.48), hostRect.height - 24));
  const cursorLeft = editorRect.left - hostRect.left + visible.left;
  const cursorTop = editorRect.top - hostRect.top + visible.top;
  const left = Math.max(12, Math.min(cursorLeft - 26, hostRect.width - width - 12));
  const below = cursorTop + visible.height + 10;
  const showBelow = below + height <= hostRect.height - 12;
  const top = showBelow
    ? below
    : Math.max(12, Math.min(cursorTop - height - 10, hostRect.height - height - 12));
  return {
    left,
    top,
    width,
    height,
    caretLeft: Math.max(18, Math.min(cursorLeft - left, width - 18)),
    caretSide: showBelow ? "top" : "bottom",
  };
}
function childEntityPeekAnchor(parent: EntityPeek, current?: EditorHandle): EntityPeek["anchor"] {
  const base = entityPeekAnchor(current);
  const host = document.querySelector<HTMLElement>(".editors");
  const parentNode = host
    ? [...host.querySelectorAll<HTMLElement>(".entity-peek")]
        .find((node) => node.dataset.entityPeekId === parent.id)
    : undefined;
  if (!host || !parentNode)
    return { ...base, left: base.left + 28, top: base.top + 48, caretLeft: 20, caretSide: "top" };
  const hostRect = host.getBoundingClientRect();
  const parentRect = parentNode.getBoundingClientRect();
  const parentLeft = parentRect.left - hostRect.left;
  const parentTop = parentRect.top - hostRect.top;
  const width = Math.max(300, Math.min(base.width, parentRect.width - 24, hostRect.width - 24));
  const left = Math.max(12, Math.min(parentLeft + 28, hostRect.width - width - 12));
  const below = parentTop + parentRect.height + 12;
  const fitsBelow = below + base.height <= hostRect.height - 12;
  return {
    ...base,
    left,
    top: fitsBelow
      ? below
      : Math.max(12, Math.min(parentTop + 44, hostRect.height - base.height - 12)),
    width,
    caretLeft: Math.max(18, Math.min(parentLeft + 46 - left, width - 18)),
    caretSide: "top",
  };
}
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
  const [diagnosticsByDocument, setDiagnosticsByDocument] = useState<Record<string, EditorDiagnostic[]>>({});
  const [controllerNotice, setControllerNotice] = useState<{ id: string; message: string }>();
  const [settings, setSettings] = useState<Settings>(initialSettings),
    [projects, setProjects] = useState<Project[]>([]),
    [snippets, setSnippets] = useState<Snippet[]>([]);
  const [paneIds, setPaneIds] = useState(["", ""]),
    [focusedPane, setFocusedPane] = useState(0);
  const paneRef = useRef(0);
  useEffect(() => setEditorSnippets(snippets), [snippets]);
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
  const [checkMailOpen, setCheckMailOpen] = useState(false);
  function closeCheckMail() {
    setCheckMailOpen(false);
    if (selectedTool === "check-mail") setSelectedTool(undefined);
  }
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
    [running, setRunning] = useState(false),
    [queryElapsedMs, setQueryElapsedMs] = useState<number | null>(null);
  const queryTimer = useRef<number>();
  const [hits, setHits] = useState<Hit[]>([]),
    [search, setSearch] = useState(""),
    [fileName, setFileName] = useState(""),
    [searchSubmitted, setSearchSubmitted] = useState(false),
    [regex, setRegex] = useState(false),
    [matchCase, setMatchCase] = useState(false),
    [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState("");
  const [searchStopped, setSearchStopped] = useState(false);
  const submitSearch = useRef<() => void>();
  const stopSearch = useRef<() => void>();
  const [objects, setObjects] = useState<Table>(),
    [objectQuery, setObjectQuery] = useState(""),
    [objectOffset, setObjectOffset] = useState(0),
    [contentSearch, setContentSearch] = useState(false),
    [dbBusy, setDbBusy] = useState(false);
  const [menuKind, setMenuKind] = useState<"web" | "mobile">("web");
  const [commandsByKind, setCommandsByKind] = useState<Record<"web" | "mobile", CommandNode[]>>({ web: [], mobile: [] });
  const commands = commandsByKind[menuKind];
  const [menuQueries, setMenuQueries] = useState<Record<"web" | "mobile", string>>({ web: "", mobile: "" });
  const menuQuery = menuQueries[menuKind];
  const setMenuQuery = (value: string) => setMenuQueries((current) => ({ ...current, [menuKind]: value }));
  const [menuLoaded, setMenuLoaded] = useState<Record<"web" | "mobile", boolean>>({ web: false, mobile: false });
  const [menuLoadingByKind, setMenuLoadingByKind] = useState<Record<"web" | "mobile", boolean>>({ web: false, mobile: false });
  const menuLoading = menuLoadingByKind[menuKind];
  const [menuLoadErrors, setMenuLoadErrors] = useState<Record<"web" | "mobile", string>>({ web: "", mobile: "" });
  const menuLoadError = menuLoadErrors[menuKind];
  const [menuIndex, setMenuIndex] = useState(-1),
    [controllerRequests, setControllerRequests] = useState(0),
    [controllers, setControllers] = useState<ControllerGroup[]>([]),
    [expandedController, setExpandedController] = useState<string>();
  const controller = controllers.find((entry) => entry.id === expandedController);
  const controllerRequest = useRef(0);
  const controllerSelection = useRef(0);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [history, setHistory] = useState<{ projectId: string; files: string[] }[]>();
  const historyRemovalQueue = useRef(new Map<string, Set<string>>());
  const historyRemovalTimers = useRef(new Map<string, number>());
  const [sourceCollection, setSourceCollection] = useState<{ samples: string[]; destination: string }>();
  const [historySearch, setHistorySearch] = useState("");
  const historyResults = useMemo(() => {
    const query = historySearch.trim().toLowerCase();
    const current = history?.filter(entry => entry.projectId === project?.id);
    if (!query) return current;
    return current?.flatMap((entry) => {
      const files = entry.files.filter((path) =>
        path.split(/[\\/]/).slice(-2).some((part) => part.toLowerCase().includes(query)));
      return files.length ? [{ ...entry, files }] : [];
    });
  }, [history, historySearch, project?.id]);
  const switchingHistoryProject = useRef(false);
  const [projectActionBusy, setProjectActionBusy] = useState(false);
  const [structureClear, setStructureClear] = useState<{ id: string; path: string; count: number }>();
  const [searchFolder, setSearchFolder] = useState("");
  const [searchScope, setSearchScope] = useState<{ workspace: string; folders: Entry[] }>();
  useEffect(() => {
    if (!workspace || nav !== "search" || !side) return;
    let active = true;
    const list = (path: string) => request<Entry[]>("workspace.list", { path });
    void list(workspace).then(async entries => {
      const appData = entries.filter(entry => entry.directory && /^app_?data$/i.test(entry.name));
      const controllers = (await Promise.allSettled(appData.map(entry => list(entry.path))))
        .flatMap(result => result.status === "fulfilled" ? result.value : [])
        .filter(entry => entry.directory && /^controllers?$/i.test(entry.name));
      const folders = (await Promise.allSettled(controllers.map(entry => list(entry.path))))
        .flatMap(result => result.status === "fulfilled" ? result.value : [])
        .filter(entry => entry.directory)
        .sort((left, right) => left.name.localeCompare(right.name) || left.path.localeCompare(right.path));
      if (active) setSearchScope({ workspace, folders });
    }).catch(() => {
      if (active) setSearchScope({ workspace, folders: [] });
    });
    return () => { active = false; };
  }, [workspace, nav, side]);
  const searchFolders = searchScope?.workspace === workspace ? searchScope.folders : [];
  const selectedSearchFolder = searchFolders.find(entry => entry.path === searchFolder);
  const searchRoot = selectedSearchFolder?.path ?? workspace;
  const [unsavedPrompt, setUnsavedPrompt] = useState<{
    names: string[];
    resolve: (choice: "save" | "discard" | "cancel") => void;
  }>();
  const askUnsaved = (names: string[]) =>
    new Promise<"save" | "discard" | "cancel">((resolve) =>
      setUnsavedPrompt({ names, resolve }),
    );
  const [peeks, setPeeks] = useState<EntityPeek[]>([]);
  const peeksRef = useRef<EntityPeek[]>([]);
  peeksRef.current = peeks;
  const resultsByTab = useRef(new Map<string, QueryResult>());
  const queryDurationByTab = useRef(new Map<string, number>());
  const [external, setExternal] = useState<Tab>();
  const primaryEditor = useRef<EditorHandle>();
  const peekEditors = useRef(new Map<string, EditorHandle>());
  const peekMountKeys = useRef(new Map<string, string>());
  const commandEntityPeekId = useRef<string>();
  useEffect(() => {
    if (!peeks.length) return;
    const insidePeek = (target: EventTarget | null) =>
      target instanceof Element && Boolean(target.closest(".entity-peek"));
    const blockWheel = (event: WheelEvent) => {
      if (!insidePeek(event.target)) event.preventDefault();
    };
    const blockTouchMove = (event: TouchEvent) => {
      if (!insidePeek(event.target)) event.preventDefault();
    };
    document.body.classList.add("entity-peek-scroll-locked");
    window.addEventListener("wheel", blockWheel, { capture: true, passive: false });
    window.addEventListener("touchmove", blockTouchMove, { capture: true, passive: false });
    return () => {
      document.body.classList.remove("entity-peek-scroll-locked");
      window.removeEventListener("wheel", blockWheel, true);
      window.removeEventListener("touchmove", blockTouchMove, true);
    };
  }, [peeks.length > 0]);
  const scriptToFocus = useRef<string>();
  const editor = useRef<EditorHandle>(),
    latest = useRef({ tabs, activeId, settings, projectId: project?.id }),
    runRef = useRef<ReturnType<typeof operation<QueryResult>>>(),
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
    controllerDiagnosticsGeneration = useRef(new Map<string, number>()),
    controllerDiagnosticsTimers = useRef(new Map<string, number>()),
    defaultTarget = useRef<Target>(),
    projectSysTarget = useRef<Target>(),
    projectTargetIds = useRef<string[]>([]),
    pendingOpens = useRef(new Map<string, Promise<void>>());
  latest.current = { tabs, activeId, settings, projectId: project?.id };
  const active = tabs.find((t) => t.id === activeId),
    target = active?.target ?? defaultTarget.current,
    diagnostics = active ? diagnosticsByDocument[active.id] ?? [] : [];
  useEffect(() => {
    if (!selectedTool && active && language(active.name) === "xml") setPanel(false);
  }, [active?.id, active?.name, selectedTool]);
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
  const [selectedCommands, setSelectedCommands] = useState<Record<"web" | "mobile", string>>({ web: "", mobile: "" });
  const selectedCommand = selectedCommands[menuKind];
  const setSelectedCommand = (id: string) => setSelectedCommands((current) => ({ ...current, [menuKind]: id }));
  const [menuEditor, setMenuEditor] = useState<{ command: CommandNode; target: Target; appTarget?: Target; root: string }>();
  const [menuContext, setMenuContext] = useState<{ command: CommandNode; x: number; y: number }>();
  const [menuScriptChoice, setMenuScriptChoice] = useState<CommandNode>();
  const [expandedMenus, setExpandedMenus] = useState({ web: new Set<string>(), mobile: new Set<string>() });
  const menuScroll = useRef({ web: 0, mobile: 0 });
  const restoreMenuScroll = useCallback((element: HTMLDivElement | null) => {
    if (element) element.scrollTop = menuScroll.current[menuKind];
  }, [menuKind]);
  const [catalogTemplate, setCatalogTemplate] = useState("");
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
    const sys = projectSysTarget.current;
    const app = defaultTarget.current;
    setMenuEditor({ command, target: sys, appTarget: app?.id === sys.id ? undefined : app, root: workspace });
    setSelectedTool("menu-editor");
  }
  function openMenuInsertScript(command: CommandNode, includeChildren?: boolean) {
    const sysTarget = projectSysTarget.current;
    if (!sysTarget) { setStatus("Dự án chưa có kết nối Sys."); return; }
    setMenuContext(undefined);
    if (command.children.length && includeChildren === undefined) {
      setMenuScriptChoice(command);
      return;
    }
    setMenuScriptChoice(undefined);
    void guard(async () => {
      const script = await request<string>("project.menu.insertScript", { targetId: sysTarget.id, wmenuId: command.entry.id, includeChildren });
      newDocument(script, command.entry.id.replaceAll(".", "-") + "-menu.sql", sysTarget);
      setStatus("Đã tạo script INSERT cho wcommand và command.");
    });
  }
  useEffect(() => {
    if (!menuContext) return;
    const close = () => setMenuContext(undefined);
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") close(); };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", escape);
    return () => {
      document.removeEventListener("pointerdown", close);
      document.removeEventListener("keydown", escape);
    };
  }, [menuContext]);
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
  }, [workspace]);
  useEffect(() => {
    setHits([]);
    setSearchError("");
    setSearchStopped(false);
    setSearching(false);
    setSearchSubmitted(false);
    if (!side || nav !== "search" || !searchRoot || (!search.trim() && !fileName.trim())) return;
    let pending: ReturnType<typeof operation<Hit[]>> | undefined;
    const unsubscribe = subscribe(event => {
      if (event.type === "workspace.search.progress" && pending && event.requestId === pending.id && event.hits) {
        const batch = event.hits;
        setHits(current => [...current, ...batch]);
      }
    });
    const find = () => {
      if (pending) return;
      setSearchSubmitted(true);
      setHits([]);
      setSearching(true);
      setSearchError("");
      setSearchStopped(false);
      const op = operation<Hit[]>("workspace.search", {
        path: searchRoot, query: search.trim() ? search : "", fileName: fileName.trim(), regex, matchCase, firstPerFile: true, stream: true,
        buffers: Object.fromEntries(latest.current.tabs.flatMap(tab => {
          if (!tab.path || !pathIsWithin(tab.path, searchRoot)) return [];
          const text = textOf(tab);
          return text === tab.saved ? [] : [[tab.path, text]];
        })),
      });
      pending = op;
      void op.promise.then(found => {
        if (pending === op) setHits(found);
      }).catch((error: Error & { code?: string }) => {
        if (pending === op && error.code !== "cancelled") setSearchError(error.message);
      }).finally(() => {
        if (pending === op) {
          pending = undefined;
          setSearching(false);
        }
      });
    };
    const cancel = () => {
      void pending?.cancel().catch(() => {});
      pending = undefined;
    };
    submitSearch.current = find;
    stopSearch.current = () => {
      cancel();
      setSearching(false);
      setSearchStopped(true);
    };
    return () => {
      unsubscribe();
      submitSearch.current = undefined;
      stopSearch.current = undefined;
      cancel();
    };
  }, [side, nav, searchRoot, search, fileName, regex, matchCase]);
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
  const queueHistoryRemoval = useCallback((projectId: string, path: string) => {
    setHistory((current) => current?.flatMap((entry) => {
      if (entry.projectId !== projectId) return [entry];
      const files = entry.files.filter((file) => file.toLowerCase() !== path.toLowerCase());
      return files.length ? [{ ...entry, files }] : [];
    }));
    const queued = historyRemovalQueue.current.get(projectId) ?? new Set<string>();
    queued.add(path);
    historyRemovalQueue.current.set(projectId, queued);
    const oldTimer = historyRemovalTimers.current.get(projectId);
    if (oldTimer !== undefined) window.clearTimeout(oldTimer);
    historyRemovalTimers.current.set(projectId, window.setTimeout(() => {
      historyRemovalTimers.current.delete(projectId);
      const paths = [...(historyRemovalQueue.current.get(projectId) ?? [])];
      historyRemovalQueue.current.delete(projectId);
      if (!paths.length) return;
      void guard(async () => {
        try {
          await request("project.history.removeMany", { id: projectId, paths });
        } catch (error) {
          setHistoryRevision((version) => version + 1);
          throw error;
        }
      });
    }, 140));
  }, [guard]);
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
          project: latest.current.projectId,
        },
        connection ?? defaultTarget.current ?? (latest.current.projectId
          ? undefined
          : latest.current.tabs.find((t) => t.id === latest.current.activeId)?.target ?? targets[0]),
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
            added.push({ ...document, saved: document.text, target: defaultTarget.current ??
              (latest.current.projectId ? undefined : targets[0]) });
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
          if (d) addDocument({ ...d, project: projectId }, defaultTarget.current ??
            (projectId || latest.current.projectId ? undefined : targets[0]));
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
    syncSavedText(saved, text);
    setTabs((old) =>
      old.map((t) =>
        t.id === saved.id
          ? {
              ...t,
              ...saved,
              text: textOf(saved),
              version: Math.max(t.version, saved.version),
              saved: saved.text,
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
  const closingSession = useRef(false);
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
    if (closingSession.current) return;
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
    closingSession.current = true;
    try {
      await request("recovery.save", { documents: [] });
      await request("window.close");
    } finally {
      closingSession.current = false;
    }
  }, []);
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
      setWorkspace(b.settings.workspace ?? "");
      if (bootstrap) {
        setSnippets(b.snippets);
        void request<Snippet[]>("snippet.list")
          .then((loaded) => setSnippets((current) => current === b.snippets ? loaded : current))
          .catch(() => {});
      } else setSnippets(b.snippets);
      const selected = b.projects.find((p) => p.id === initialProjectId);
      const opening = guard(async () => {
        if (!selected) return;
        const start = performance.now();
        await openProject(selected);
        performance.measure("startup.project", { start, end: performance.now() });
      }).finally(() => setOpeningInitialProject(false));
      const recovery = await guard(() => request<
        {
          name: string;
          text: string;
          encoding: string;
          bom: boolean;
          newline: string;
        }[]
      >("recovery.load")) ?? [];
      await opening;
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
    applyButtonTheme(settings.buttonColors?.[settings.theme]);
  }, [settings.theme, settings.buttonColors]);
  useEffect(() => {
    const currentResult = resultsByTab.current.get(activeId);
    setResult(currentResult);
    setQueryElapsedMs(queryDurationByTab.current.get(activeId) ?? currentResult?.durationMs ?? null);
    setResultIndex(0);
  }, [activeId]);
  useEffect(() => () => window.clearInterval(queryTimer.current), []);
  useEffect(() => {
    if (!native || !ready) return;
    const timer = setInterval(() => {
      if (!closingSession.current) void guard(persistRecovery);
    }, 8000);
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
  const projectConnectionLoad = useRef<Promise<Target | undefined>>();
  const projectOpeningVersion = useRef(0);
  const waitingForSqlApp = useRef<number>();
  async function runSql() {
    const version = projectOpeningVersion.current;
    if (!active || runRef.current || waitingForSqlApp.current === version || closingSession.current) return;
    if (!active.target && active.project && active.project !== project?.id) {
      setStatus("Hãy chọn kết nối cho tab SQL thuộc dự án khác.");
      return;
    }
    const selection = editor.current?.getSelection();
    const selected = selection
      ? editor.current?.getModel()?.getValueInRange(selection)
      : "";
    const sql = selected || textOf(active);
    let sqlTarget = active.target ?? defaultTarget.current;
    if (!sqlTarget && projectConnectionLoad.current) {
      waitingForSqlApp.current = version;
      setStatus("Đang chờ kết nối SQL App…");
      try { sqlTarget = await projectConnectionLoad.current; }
      finally {
        if (waitingForSqlApp.current === version) waitingForSqlApp.current = undefined;
      }
      if (version !== projectOpeningVersion.current || latest.current.activeId !== active.id || closingSession.current) return;
      sqlTarget = latest.current.tabs.find(tab => tab.id === active.id)?.target ?? sqlTarget;
    }
    if (!sqlTarget) {
      if (project) setStatus("Chưa kết nối được SQL App của dự án. Kiểm tra cấu hình kết nối.");
      else setDialog("connection");
      return;
    }
    if (!active.target)
      setTabs(current => current.map(tab => tab.id === active.id ? { ...tab, target: sqlTarget } : tab));
    const startedAt = performance.now();
    setRunning(true);
    setQueryElapsedMs(0);
    window.clearInterval(queryTimer.current);
    queryTimer.current = window.setInterval(() => setQueryElapsedMs(performance.now() - startedAt), 100);
    setPanel(true);
    setBottom("results");
    setStatus("Đang chạy SQL…");
    setError("");
    markSqlErrors(active, []);
    const queryDocument = active;
    const targetId = sqlTarget.id;
    queryDurationByTab.current.delete(queryDocument.id);
    const op = operation<QueryResult>("sql.execute", {
      targetId,
      sql,
    });
    runRef.current = op;
    try {
      const r = await op.promise;
      resultsByTab.current.set(queryDocument.id, r);
      queryDurationByTab.current.set(queryDocument.id, r.durationMs);
      if (latest.current.activeId === queryDocument.id) {
        setResult(r);
        setResultIndex(0);
        setQueryElapsedMs(r.durationMs);
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
        setStatus("SQL gặp lỗi");
        if (failure.details)
          markSqlErrors(
            queryDocument,
            failure.details,
            selected ? selection?.startLineNumber : 1,
            selected ? selection?.startColumn : 1,
          );
      }
      const elapsed = performance.now() - startedAt;
      queryDurationByTab.current.set(queryDocument.id, elapsed);
      if (latest.current.activeId === queryDocument.id) setQueryElapsedMs(elapsed);
    } finally {
      invalidateTableCatalog(targetId);
      window.clearInterval(queryTimer.current);
      queryTimer.current = undefined;
      if (latest.current.activeId !== queryDocument.id)
        setQueryElapsedMs(queryDurationByTab.current.get(latest.current.activeId)
          ?? resultsByTab.current.get(latest.current.activeId)?.durationMs ?? null);
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
  function releaseEntityPeek(value: EntityPeek) {
    disposeModel(value.snippet);
    disposeModel(value.source);
    peekEditors.current.delete(value.id);
    peekMountKeys.current.delete(value.id);
    void request("document.close", { id: value.source.id }).catch(() => {});
  }
  function replaceEntityPeeks(next: EntityPeek[]) {
    peeksRef.current = next;
    setPeeks(next);
  }
  function closeEntityPeek(id?: string) {
    const current = peeksRef.current;
    const index = id ? current.findIndex((entry) => entry.id === id) : current.length - 1;
    if (index < 0) return;
    const retained = current.slice(0, index);
    const closing = current.slice(index);
    replaceEntityPeeks(retained);
    requestAnimationFrame(() => {
      closing.forEach(releaseEntityPeek);
      const parent = retained.at(-1);
      if (parent) peekEditors.current.get(parent.id)?.focus();
      else editor.current?.focus();
    });
  }
  function includeBuffers(extra?: DocumentInfo) {
    return Object.fromEntries([
      ...tabs.filter((tab) => tab.path).map((tab) => [tab.path!, textOf(tab)] as const),
      ...(extra?.path ? [[extra.path, extra.text] as const] : []),
    ]);
  }
  async function findInclude(
    path: string,
    text: string,
    offset: number,
    contextPath: string,
    contextText: string,
    extra?: DocumentInfo,
  ) {
    return request<IncludeLocation | null>("xml.include", {
      path,
      text,
      offset,
      contextPath,
      contextText,
      buffers: includeBuffers(extra),
    });
  }
  async function showEntityPeek(
    found: IncludeLocation,
    contextPath: string,
    contextText: string,
    anchor: EntityPeek["anchor"],
    parent?: EntityPeek,
  ) {
    const opened = await request<DocumentInfo>("document.open", { path: found.path });
    const buffer = tabs.find((tab) => tab.path?.toLowerCase() === found.path.toLowerCase());
    const source = buffer ? { ...opened, text: textOf(buffer) } : opened;
    const rangeStart = Number.isInteger(found.start)
      ? Math.max(0, Math.min(found.start, source.text.length))
      : 0;
    const rangeLength = Number.isInteger(found.length)
      ? Math.max(0, Math.min(found.length, source.text.length - rangeStart))
      : source.text.length - rangeStart;
    const location = {
      ...found,
      name: found.name || source.name,
      start: rangeStart,
      length: rangeLength,
    };
    const snippet: DocumentInfo = {
      ...source,
      id: "entity-peek-" + crypto.randomUUID(),
      text: source.text.slice(rangeStart, rangeStart + rangeLength),
      version: 1,
    };
    const id = "entity-peek-" + crypto.randomUUID();
    const next: EntityPeek = {
      id,
      parentId: parent?.id,
      level: parent ? parent.level + 1 : 0,
      source,
      snippet,
      location,
      contextPath,
      contextText,
      fullFile: false,
      compactHeight: Math.min(
        anchor.height,
        Math.max(132, 94 + snippet.text.split(/\r\n|\r|\n/).length * Math.round((settings.editorFontSize ?? 14) * 1.65)),
      ),
      anchor,
    };
    const current = peeksRef.current;
    const parentIndex = parent ? current.findIndex((entry) => entry.id === parent.id) : -1;
    if (parent && parentIndex < 0) {
      releaseEntityPeek(next);
      return;
    }
    const retained = parent ? current.slice(0, parentIndex + 1) : [];
    const discarded = parent ? current.slice(parentIndex + 1) : current;
    replaceEntityPeeks([...retained, next]);
    if (discarded.length)
      requestAnimationFrame(() => discarded.forEach(releaseEntityPeek));
  }
  async function navigateFromEntityPeek(id: string, openInEditor: boolean) {
    const current = peeksRef.current.find((entry) => entry.id === id);
    const currentEditor = peekEditors.current.get(id);
    if (!current || !currentEditor || !current.source.path) return;
    const displayed = current.fullFile ? current.source : current.snippet;
    const model = modelFor(displayed);
    const localOffset = model.getOffsetAt(
      currentEditor.getPosition() ?? { lineNumber: 1, column: 1 },
    );
    const offset = current.fullFile ? localOffset : current.location.start + localOffset;
    const found = await findInclude(
      current.source.path,
      current.source.text,
      offset,
      current.contextPath,
      current.contextText,
      current.source,
    );
    if (!found) throw new Error("Không tìm thấy entity tại vị trí con trỏ.");
    if (openInEditor) {
      closeEntityPeek(peeksRef.current[0]?.id);
      await open(found.path, found.line, found.column);
    } else {
      await showEntityPeek(
        found,
        current.contextPath,
        current.contextText,
        childEntityPeekAnchor(current, currentEditor),
        current,
      );
    }
  }
  const execute = async (id: string) => {
    if (selectedTool && /^(editor\.|sql\.|file\.save|xml\.include)/.test(id) && !(id === "sql.run" && tableTab)) return;
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
        else if (selectedTool === "check-mail") closeCheckMail();
        else if (selectedTool === "template-studio") closeStudio();
        else if (tableTab) closeTableTab(tableTab.id);
        else if (active) await closeTab(active);
        break;
      case "profiler":
        setProfilerTab((existing) => existing ?? { target: tableTab?.target ?? target });
        setSelectedTool("profiler");
        break;
      case "check-mail":
        setCheckMailOpen(true);
        setSelectedTool("check-mail");
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
      case "request.command":
        setDialog("request.command");
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
        const focusedPeek = document.activeElement instanceof HTMLElement
          ? document.activeElement.closest<HTMLElement>(".entity-peek")?.dataset.entityPeekId
          : undefined;
        const peekId = commandEntityPeekId.current ?? focusedPeek;
        if (peekId && peeksRef.current.some((entry) => entry.id === peekId)) {
          await navigateFromEntityPeek(peekId, id === "xml.include.open");
          break;
        }
        if (!active?.path)
          throw new Error("Lưu file trước khi điều hướng include.");
        const model = modelFor(active);
        const activeText = textOf(active);
        const found = await findInclude(
          active.path,
          activeText,
          model.getOffsetAt(
            editor.current?.getPosition() ?? { lineNumber: 1, column: 1 },
          ),
          active.path,
          activeText,
        );
        if (!found)
          throw new Error("Không tìm thấy entity tại vị trí con trỏ.");
        if (id === "xml.include") {
          await showEntityPeek(
            found,
            active.path,
            activeText,
            entityPeekAnchor(editor.current),
          );
        } else {
          if (peeksRef.current.length) closeEntityPeek(peeksRef.current[0].id);
          await open(found.path, found.line, found.column);
        }
        break;
      }
      case "file.save":
        await save();
        break;
      case "file.saveAs":
        await save(undefined, true);
        break;
      case "sql.run":
        if (tableTab) document.getElementById(tableTab.id + "-panel")
          ?.querySelector<HTMLButtonElement>(".table-run-button")?.click();
        else await runSql();
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
          key === "F8" && e.target.closest(".table-data-grid") ||
          key === "Ctrl+G" && e.target.closest(".table-inspection") ||
          key === "Ctrl+S" && e.target.closest(".table-inspection"))) return;
      if (key === "Ctrl+G" && tableTab) {
        e.preventDefault();
        document.getElementById(tableTab.id + "-panel")?.querySelector<HTMLElement>(".table-inspection")
          ?.dispatchEvent(new KeyboardEvent("keydown", { key: "g", ctrlKey: true, bubbles: true }));
        return;
      }
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
        if (c[0] !== "window.new" || !e.repeat) {
          commandEntityPeekId.current = e.target instanceof HTMLElement
            ? e.target.closest<HTMLElement>(".entity-peek")?.dataset.entityPeekId
            : undefined;
          void guard(async () => {
            try { await executeRef.current(c[0]); }
            finally { commandEntityPeekId.current = undefined; }
          });
        }
      }
    };
    window.addEventListener("keydown", keys, true);
    return () => window.removeEventListener("keydown", keys, true);
  }, [guard, tableTab?.id]);
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
  async function loadCommands(kind: "web" | "mobile" = menuKind, refresh = false) {
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
      setMenuLoadErrors((current) => ({ ...current, [kind]: "Mở dự án có kết nối SYS để tải chức năng." }));
      return;
    }
    setMenuLoadingByKind((current) => ({ ...current, [kind]: true }));
    setMenuLoadErrors((current) => ({ ...current, [kind]: "" }));
    try {
      const loaded = await request<CommandNode[]>("project.commands", {
        targetId: sys.id,
        appDatabase: defaultTarget.current?.database ?? target?.database ?? "",
        kind,
        refresh,
        context: project?.id ?? workspace,
      });
      if (context === projectTargetIds.current.join("|")) {
        setCommandsByKind((current) => ({ ...current, [kind]: loaded }));
        setMenuLoaded((current) => ({ ...current, [kind]: true }));
        if (kind === "mobile" && loaded.length === 0) setMenuKind("web");
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (context === projectTargetIds.current.join("|"))
        setMenuLoadErrors((current) => ({ ...current, [kind]: message }));
      throw error;
    } finally {
      if (context === projectTargetIds.current.join("|"))
        setMenuLoadingByKind((current) => ({ ...current, [kind]: false }));
    }
  }
  function switchMenuKind(kind: "web" | "mobile") {
    setMenuKind(kind);
    setMenuIndex(-1);
    setMenuContext(undefined);
    if (!menuLoaded[kind]) void guard(() => loadCommands(kind));
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
  async function refreshControllerDiagnostics(
    id: string,
    root: string,
    files: string[],
    showLoading = true,
  ) {
    const generation = (controllerDiagnosticsGeneration.current.get(id) ?? 0) + 1;
    controllerDiagnosticsGeneration.current.set(id, generation);
    if (showLoading)
      setControllers((current) => current.map((entry) =>
        entry.id === id ? { ...entry, diagnosticsLoading: true } : entry));
    try {
      const response = await operation<{ diagnostics: ControllerProblem[] }>(
        "project.controllerDiagnostics",
        { root, files, buffers: xmlIncludeBuffers() },
        20_000,
      ).promise;
      if (controllerDiagnosticsGeneration.current.get(id) !== generation ||
          root !== latest.current.settings.workspace) return;
      const controllerPaths = new Set(files.map(diagnosticPathKey));
      const problems = (response?.diagnostics ?? []).filter((problem) =>
        problem.path && controllerPaths.has(diagnosticPathKey(problem.path)));
      setControllers((current) => current.map((entry) =>
        entry.id === id ? { ...entry, problems, diagnosticsLoading: false } : entry));
      const first = problems[0];
      const message = first
        ? `${first.message} Dòng ${first.line}, cột ${first.column}. Đường dẫn: ${first.path}` +
          (problems.length > 1 ? ` Còn ${problems.length - 1} file lỗi khác.` : "")
        : "";
      setControllerNotice((current) => showLoading
        ? first ? { id, message } : current?.id === id ? undefined : current
        : current?.id === id ? first ? { id, message } : undefined : current);
    } catch (problem) {
      if (controllerDiagnosticsGeneration.current.get(id) !== generation) return;
      setControllers((current) => current.map((entry) =>
        entry.id === id ? { ...entry, diagnosticsLoading: false } : entry));
      setStatus("Không kiểm tra được khai báo controller: " + (problem as Error).message);
    }
  }
  function updateDocumentDiagnostics(doc: Tab, next: EditorDiagnostic[]) {
    setDiagnosticsByDocument((current) => ({ ...current, [doc.id]: next }));
    if (!doc.path) return;
    const fileKey = diagnosticPathKey(doc.path);
    const errors: ControllerProblem[] = next
      .filter((problem) => problem.severity === "error")
      .map((problem) => ({ ...problem, path: doc.path! }));
    setControllers((current) => current.map((entry) =>
      entry.files.some((path) => diagnosticPathKey(path) === fileKey)
        ? {
            ...entry,
            problems: [
              ...entry.problems.filter((problem) => diagnosticPathKey(problem.path) !== fileKey),
              ...errors,
            ],
          }
        : entry));
    for (const group of controllers.filter((entry) =>
      entry.files.some((path) => diagnosticPathKey(path) === fileKey))) {
      const previous = controllerDiagnosticsTimers.current.get(group.id);
      if (previous !== undefined) window.clearTimeout(previous);
      controllerDiagnosticsTimers.current.set(group.id, window.setTimeout(() => {
        controllerDiagnosticsTimers.current.delete(group.id);
        void refreshControllerDiagnostics(group.id, latest.current.settings.workspace ?? "", group.files, false);
      }, 650));
    }
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
    setControllers((current) => {
      const previous = current.find((entry) => entry.id === id);
      const group: ControllerGroup = {
        id, link, ...found, employeeTab: employeeTab || previous?.employeeTab, sysId,
        problems: previous?.problems ?? [], diagnosticsLoading: true,
      };
      return previous
        ? current.map((entry) => entry.id === id ? group : entry)
        : [...current, group];
    });
    void refreshControllerDiagnostics(id, root, found.files);
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
    void guard(() => openController(node.entry.link, false, node.entry.sysId, true));
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
  async function clearProjectHistory(owner?: Project) {
    await request("project.history.clear", { id: owner?.id });
    setHistory(current => owner ? current?.filter(entry => entry.projectId !== owner.id) : []);
    setHistoryRevision(version => version + 1);
    setStatus(owner ? "Đã xóa lịch sử của " + owner.name : "Đã xóa lịch sử của tất cả dự án");
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
    const version = ++projectOpeningVersion.current;
    const opened = await operation<{
      project: Project;
      targets: Target[];
      targetsDeferred?: boolean;
    }>("project.open", { id: p.id, deferTargets: true }, 31_000).promise;
    if (version !== projectOpeningVersion.current) return;
    setProject(opened.project);
    setActiveId("");
    const oldTargetIds = [...projectTargetIds.current, ...closingTargets];
    defaultTarget.current = undefined;
    projectSysTarget.current = undefined;
    projectTargetIds.current = [];
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
    setTargets(old => old.filter(target => !oldTargetIds.includes(target.id)));
    setCommandsByKind({ web: [], mobile: [] });
    setMenuLoaded({ web: false, mobile: false });
    setMenuKind("web");
    setExpandedMenus({ web: new Set(), mobile: new Set() });
    menuScroll.current = { web: 0, mobile: 0 };
    setMenuScriptChoice(undefined);
    setMenuQueries({ web: "", mobile: "" });
    setSelectedCommands({ web: "", mobile: "" });
    setMenuLoadErrors({ web: "", mobile: "" });
    setMenuLoadingByKind({ web: false, mobile: false });
    setObjects(undefined);
    controllerRequest.current++;
    setControllers([]);
    setExpandedController(undefined);
    setDialog("");
    const loading = opened.targetsDeferred
      ? operation<Target[]>("project.targets", { id: p.id }, 31_000).promise
      : Promise.resolve(opened.targets);
    projectConnectionLoad.current = loading.then(loadedTargets => {
      if (version !== projectOpeningVersion.current) return undefined;
      const app = loadedTargets[0];
      const sys = loadedTargets[1];
      defaultTarget.current = app;
      projectTargetIds.current = loadedTargets.map(item => item.id);
      setTargets(old => [...old, ...loadedTargets]);
      if (app) setSettings(old => ({ ...old, database: app.database }));
      projectSysTarget.current = sys;
      if (sys) {
        void request<CommandNode[]>("project.commands", {
          targetId: sys.id,
          appDatabase: app?.database ?? "",
          kind: "mobile",
          context: p.id,
        }).then((loaded) => {
          if (version === projectOpeningVersion.current) {
            setCommandsByKind((current) => ({ ...current, mobile: loaded }));
            setMenuLoaded((current) => ({ ...current, mobile: true }));
          }
        }).catch(() => {
          // Mobile is optional. Web loading reports its own connection errors.
        });
        setMenuLoadingByKind((current) => ({ ...current, web: true }));
        void request<CommandNode[]>("project.commands", {
          targetId: sys.id,
          appDatabase: app?.database ?? "",
          kind: "web",
          context: p.id,
        })
          .then((loaded) => {
            if (version === projectOpeningVersion.current) {
              setCommandsByKind((current) => ({ ...current, web: loaded }));
              setMenuLoaded((current) => ({ ...current, web: true }));
            }
          })
          .catch((e) => {
            if (version !== projectOpeningVersion.current) return;
            setMenuLoadErrors((current) => ({ ...current, web: e.message }));
            setError(e.message);
          })
          .finally(() => {
            if (version === projectOpeningVersion.current)
              setMenuLoadingByKind((current) => ({ ...current, web: false }));
          });
      }
      return app;
    }).catch((error: Error) => {
      if (version === projectOpeningVersion.current) {
        setError(error.message);
        setStatus("Không tải được kết nối SQL App.");
      }
      return undefined;
    });
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
      ...entry,
      file: moved(entry.file),
      files: entry.files.map(moved),
      problems: entry.problems.map((problem) => ({ ...problem, path: moved(problem.path) })),
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
        <IconButton destructive label="Xóa Structure/App" disabled={!project || projectActionBusy} onClick={() => {
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
      <WorkbenchNotice
        error={error || controllerNotice?.message}
        errorTitle={error ? undefined : "Khai báo controller chưa hợp lệ"}
        onClose={() => error ? setError("") : setControllerNotice(undefined)}
      />
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
                    if (nav === "functions") {
                      if (menuKind === "web") void loadCommands("mobile", true).catch(() => {});
                      await loadCommands(menuKind, true);
                    }
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
                    onCloseProject={() => void guard(() => clearProjectHistory(owner))}
                    onCloseAllProjects={() => void guard(() => clearProjectHistory())}
                    onOpen={(path) => void guard(() => openHistoryFile(owner, path))}
                    onRemove={(path) => queueHistoryRemoval(projectId, path)} />;
                }) : <div className="explorer-message">{!project ? "Chọn dự án để xem lịch sử." : !history ? "Đang tải lịch sử…" : history.some(entry => entry.projectId === project.id) && historySearch.trim()
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
                            problemsByPath={controllerProblemsByPath(group.problems)}
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
                    submitSearch.current?.();
                  }}
                >
                  <div className="workspace-search-input">
                    <input
                      aria-label="Nội dung tìm"
                      type="search"
                      autoFocus
                      value={search}
                      onChange={(e) => setSearch(e.target.value)}
                      placeholder="Tìm theo nội dung tệp"
                    />
                    {search && <IconButton label="Xóa nội dung tìm" type="button" onClick={() => setSearch("")}><X size={14} /></IconButton>}
                  </div>
                  <input type="search" aria-label="Tên file" placeholder="Tìm theo tên file"
                    value={fileName} onChange={event => setFileName(event.target.value)} />
                  <Select label="Phạm vi tìm kiếm" value={selectedSearchFolder?.path ?? ""} floating
                    searchOptions="Tìm thư mục"
                    options={[{ value: "", label: "Toàn dự án" }, ...searchFolders.map(folder => ({
                      value: folder.path,
                      label: searchFolders.some(entry => entry !== folder && entry.name.toLowerCase() === folder.name.toLowerCase())
                        ? folder.path.slice(workspace.length).replace(/^[\\/]+/, "") : folder.name,
                    }))]}
                    onChange={setSearchFolder} />
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
                    <Button disabled={!searchRoot || searching || (!search.trim() && !fileName.trim())} type="submit">
                      Tìm
                    </Button>
                    {searching && (
                      <Button
                        type="button"
                        onClick={() => stopSearch.current?.()}
                      >
                        Dừng
                      </Button>
                    )}
                  </div>
                </form>
                <div className="side-content">
                  <div className="section-label" role={searchError ? "alert" : "status"}>
                    {searching
                      ? `Đang tìm… ${hits.length} tệp khớp`
                      : searchError || (searchStopped ? "Đã dừng tìm kiếm." : !searchSubmitted ? "Nhập tên file hoặc nội dung rồi bấm Tìm."
                        : hits.length ? `${hits.length === 2000 ? "Ít nhất " : ""}${hits.length} tệp khớp`
                          : "Không tìm thấy tệp khớp.")}
                  </div>
                  <SearchResultTree hits={hits} root={workspace}
                    onOpen={hit => void guard(() => open(hit.path, hit.line || undefined, hit.column || undefined))} />
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
                {(commandsByKind.mobile.length > 0 || menuKind === "mobile") && (
                <div className="menu-kind-tabs" role="tablist" aria-label="Loại chức năng ERP">
                  <button type="button" role="tab" aria-selected={menuKind === "web"}
                    onClick={() => switchMenuKind("web")}>Web</button>
                  <button type="button" role="tab" aria-selected={menuKind === "mobile"}
                    onClick={() => switchMenuKind("mobile")}>Mobile</button>
                </div>
                )}
                <div className="side-form">
                  <input
                    aria-label="Tìm chức năng"
                    placeholder={`Tìm ${menuKind === "web" ? "Web" : "Mobile"}: tên / sysid / mã`}
                    value={menuQuery}
                    onChange={(e) => setMenuQuery(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") nextMenu();
                    }}
                  />

                </div>
                <div ref={restoreMenuScroll} className="side-content side-wide loading-region" aria-busy={menuLoading}
                  onScroll={event => { menuScroll.current[menuKind] = event.currentTarget.scrollTop; }}>
                  <LoadingLine busy={menuLoading} label={`Đang tải chức năng ${menuKind === "web" ? "Web" : "Mobile"}…`} />
                  {menuLoadError && <div className="tree-message error-text">{menuLoadError}</div>}
                  {!menuQuery && (
                    <FunctionTree
                      nodes={commands}
                      expanded={expandedMenus[menuKind]}
                      setExpanded={update => setExpandedMenus(current => ({ ...current, [menuKind]: update(current[menuKind]) }))}
                      selected={selectedCommand}
                      onSelect={setSelectedCommand}
                      onOpen={openFunctionController}
                      onOpenSource={(node) => void guard(() => openController(node.entry.link, false, node.entry.sysId, true))}
                      onEdit={openMenuEditor}
                      onScript={openMenuInsertScript}
                      onContext={(node, x, y) => setMenuContext({ command: node, x, y })}
                      allowMenuActions={menuKind === "web"}
                    />
                  )}
                  {(menuQuery ? matches : []).map((n, i) => (
                    <div className="function-row" key={n.entry.id}>
                      <button
                        className="search-hit"
                        aria-current={
                          selectedCommand === n.entry.id || (menuQuery && i === menuIndex) ? "page" : undefined
                        }
                        onClick={(event) => {
                          setSelectedCommand(n.entry.id);
                          if (event.detail === 0 && (n.entry.link || n.entry.sysId)) openFunctionController(n);
                        }}
                        onDoubleClick={() => {
                          if (n.entry.link || n.entry.sysId) openFunctionController(n);
                        }}
                        onFocus={() => setSelectedCommand(n.entry.id)}
                        onKeyDown={(event) => {
                          if (menuKind === "web" && event.key === "F3") {
                            event.preventDefault();
                            openMenuEditor(n);
                          } else if (menuKind === "web" && event.key === "F12") {
                            event.preventDefault();
                            event.stopPropagation();
                            openMenuInsertScript(n);
                          }
                        }}
                        onContextMenu={(event) => {
                          if (menuKind !== "web") return;
                          event.preventDefault();
                          setSelectedCommand(n.entry.id);
                          setMenuContext({ command: n, x: event.clientX, y: event.clientY });
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
                  {!menuLoading && !menuLoadError && menuLoaded[menuKind] && commands.length === 0 && (
                    <div className="empty">Không có chức năng {menuKind === "web" ? "Web" : "Mobile"} trong SYS.</div>
                  )}
                  {!menuLoading && !menuLoadError && !menuLoaded[menuKind] && (
                    <div className="empty">Chọn dự án có kết nối SYS để tải chức năng.</div>
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
        {menuScriptChoice && <Modal title="Tạo script menu" onClose={() => setMenuScriptChoice(undefined)}>
          <div className="modal-copy"><p>Bạn có muốn tạo script cho các menu con của “{menuScriptChoice.entry.caption}” không?</p></div>
          <div className="modal-footer" style={{ flexWrap: "wrap" }}>
            <Button onClick={() => setMenuScriptChoice(undefined)}>Hủy</Button>
            <Button onClick={() => openMenuInsertScript(menuScriptChoice, false)}>Chỉ menu cha</Button>
            <Button variant="primary" onClick={() => openMenuInsertScript(menuScriptChoice, true)}>Cả menu con</Button>
          </div>
        </Modal>}
        {menuContext && <div className="table-action-menu explorer-action-menu" role="menu"
          style={{ left: Math.max(8, Math.min(menuContext.x, window.innerWidth - 248)), top: Math.max(8, Math.min(menuContext.y, window.innerHeight - 136)) }}
          onPointerDown={(event) => event.stopPropagation()}>
          <Button variant="ghost" onClick={() => { const command = menuContext.command; setMenuContext(undefined); openMenuEditor(command); }}>
            Sửa thông tin menu<kbd>F3</kbd>
          </Button>
          <Button variant="ghost" onClick={() => openMenuInsertScript(menuContext.command)}>
            Tạo script INSERT menu<kbd>F12</kbd>
          </Button>
          <Button variant="ghost" disabled={!menuContext.command.entry.link && !menuContext.command.entry.sysId}
            onClick={() => {
              setCatalogTemplate(menuContext.command.entry.link || menuContext.command.entry.sysId);
              setMenuContext(undefined);
              setDialog("catalog");
            }}>Dùng làm mẫu tạo danh mục</Button>
        </div>}
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
            {(checkMailOpen || watchTab || menuEditor || profilerTab || studioOpen || advanceNoteOpen || tableTabs.length > 0) && (
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
                {checkMailOpen && <div className={`document-tab ${selectedTool === "check-mail" ? "active" : ""}`}>
                  <button id="check-mail-tab" role="tab" aria-controls="check-mail-panel"
                    aria-selected={selectedTool === "check-mail"} onClick={() => setSelectedTool("check-mail")}>Check Mail</button>
                  <IconButton label="Đóng Check Mail" onClick={closeCheckMail}><X size={13} /></IconButton>
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
                <IconButton
                  label={target ? `Sao chép tên database ${target.database}` : "Chưa chọn database"}
                  disabled={!target}
                  onClick={() => target && void writeClipboard(target.database)
                    .then(() => setStatus(`Đã sao chép ${target.database}.`))
                    .catch((error: Error) => setStatus(error.message))}
                >
                  <Copy size={15} />
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
              onOpenTable={menuEditor.appTarget ? table => {
                const target = menuEditor.appTarget;
                const existing = tableTabs.find(tab => tab.target?.id === target?.id && tab.table === table);
                if (existing) {
                  setSelectedTool(existing.id);
                  return;
                }
                const id = "table-" + crypto.randomUUID();
                const number = ++tableNumber.current;
                setTableTabs(tabs => [...tabs, { id, number, target, table }]);
                setSelectedTool(id);
              } : undefined}
              onSaved={async id => {
                if (projectSysTarget.current?.id === menuEditor.target.id) {
                  await loadCommands("web", true); setSelectedCommands((current) => ({ ...current, web: id }));
                }
                setStatus("Đã lưu thông tin menu " + id);
              }} />
          </section>}
          {checkMailOpen && <section id="check-mail-panel" className="profiler-panel" role="tabpanel"
            aria-labelledby="check-mail-tab" hidden={selectedTool !== "check-mail"}>
            <Suspense fallback={<div role="status">Đang mở Check Mail…</div>}><CheckMail /></Suspense>
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
              <TableToolErrorBoundary resetKey={tab.target?.id + ":" + (tab.table ?? "")}>
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
                  const paneDiagnostics = diagnosticsByDocument[doc.id] ?? [];
                  const paneError = paneDiagnostics.find((problem) => problem.severity === "error");
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
                      {paneError && (
                        <div className="editor-diagnostic-banner" role="alert" title={doc.path ?? doc.name}>
                          <AlertTriangle size={15} aria-hidden="true" />
                          <span className="editor-diagnostic-message">{paneError.message}</span>
                          <span className="editor-diagnostic-location">
                            Dòng {paneError.line}, cột {paneError.column}
                            {doc.path ? ` · ${doc.path}` : ""}
                          </span>
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
                        onDiagnostics={(next) => updateDocumentDiagnostics(doc, next)}
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
            {peeks.length > 0 && (
              <>
                <div className="entity-peek-scroll-lock" aria-hidden="true" />
                {peeks.map((peek, index) => (
                  <aside
                    key={peek.id}
                    data-entity-peek-id={peek.id}
                    className={`entity-peek caret-${peek.anchor.caretSide} ${peek.parentId ? "entity-peek-child" : "entity-peek-root"}`}
                    role="dialog"
                    aria-modal="false"
                    aria-label={`Xem nhanh entity ${peek.location.name}`}
                    style={{
                      left: peek.anchor.left,
                      top: peek.fullFile || peek.anchor.caretSide === "top"
                        ? peek.anchor.top
                        : peek.anchor.top + peek.anchor.height - peek.compactHeight,
                      width: peek.anchor.width,
                      height: peek.fullFile ? peek.anchor.height : peek.compactHeight,
                      zIndex: 24 + index,
                    }}
                  >
                    <span
                      className="entity-peek-caret"
                      style={{ left: peek.anchor.caretLeft } as CSSProperties}
                    />
                    <header className="entity-peek-heading">
                      <div className="entity-peek-identity">
                        <strong>
                          {peek.parentId && <span className="entity-peek-branch" aria-hidden="true">↳</span>}
                          {peek.location.name || peek.source.name}
                        </strong>
                        <span title={peek.source.path ?? undefined}>{peek.source.path}</span>
                      </div>
                      <label className="entity-peek-full-file">
                        <input
                          type="checkbox"
                          checked={peek.fullFile}
                          onChange={(event) => {
                            peekMountKeys.current.delete(peek.id);
                            replaceEntityPeeks(peeksRef.current.map((current) =>
                              current.id === peek.id
                                ? { ...current, fullFile: event.target.checked }
                                : current));
                          }}
                        />
                        Hiện toàn bộ file
                      </label>
                      <Button
                        variant="ghost"
                        onClick={() => {
                          const root = peeksRef.current[0];
                          if (root) closeEntityPeek(root.id);
                          if (peek.source.path)
                            void guard(() => open(
                              peek.source.path!,
                              peek.location.line,
                              peek.location.column,
                            ));
                        }}
                      >
                        Mở trong tab
                      </Button>
                      <IconButton label={`Đóng xem nhanh entity ${peek.location.name}`} onClick={() => closeEntityPeek(peek.id)}>
                        <X size={15} />
                      </IconButton>
                    </header>
                    <div className="entity-peek-editor">
                      <CodeEditor
                        viewId={`entity-peek-${peek.id}`}
                        document={peek.fullFile ? peek.source : peek.snippet}
                        theme={settings.theme}
                        fontSize={settings.editorFontSize}
                        readOnly
                        onChange={() => {}}
                        onCursor={() => {}}
                        onMount={(instance) => {
                          peekEditors.current.set(peek.id, instance);
                          const key = `${peek.source.id}:${peek.fullFile ? "full" : "snippet"}`;
                          if (peekMountKeys.current.get(peek.id) === key) return;
                          peekMountKeys.current.set(peek.id, key);
                          instance.setPosition(peek.fullFile
                            ? { lineNumber: peek.location.line, column: peek.location.column }
                            : { lineNumber: 1, column: 1 });
                          instance.revealLineInCenter(peek.fullFile ? peek.location.line : 1);
                          instance.focus();
                        }}
                      />
                    </div>
                    <footer className="entity-peek-hint">
                      <span><kbd>F12</kbd> mở entity con</span>
                      <span><kbd>Ctrl F12</kbd> mở entity trong tab</span>
                    </footer>
                  </aside>
                ))}
              </>
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
              {!editorSearchQuery && queryElapsedMs !== null && (
                <span className="query-duration" role="status" aria-label="Thời gian chạy query">
                  {running ? "Đang chạy" : "Thời gian"} · {formatQueryDuration(queryElapsedMs)}
                </span>
              )}
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
              <Button variant="destructive" disabled={projectActionBusy} onClick={() => runProjectAction(async () => {
                const count = await request<number>("project.structure.clear", { id: structureClear.id, path: structureClear.path });
                controllerCache.current.clear();
                setControllers((current) => current.map((entry) => ({
                  ...entry,
                  files: entry.files.filter((file) => !pathIsWithin(file, structureClear.path)),
                  problems: entry.problems.filter((problem) => !pathIsWithin(problem.path, structureClear.path)),
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
      {dialog === "request.command" && <CommandBox onClose={() => setDialog("")}
        onOpenRequestTracking={() => setDialog("request.tracking")} />}
      {dialog === "request.tracking" && <RequestTracking targets={targets} defaultTargetId={target?.id}
        onClose={() => setDialog("")} />}
      {dialog && !["connection", "projects", "settings", "editor.sections", "editor.controllerPreview", "request.command", "request.tracking"].includes(dialog) && (
        <ToolDialog
          onDirtyChange={(dirty) => {
            toolDirty.current = dirty;
          }}
          kind={dialog}
          target={target}
          workspaceRoot={dialog === "catalog" ? project?.sourcePath || workspace : undefined}
          catalogTemplate={dialog === "catalog" ? catalogTemplate : undefined}
          catalogApp={dialog === "catalog" ? defaultTarget.current : undefined}
          catalogSys={dialog === "catalog" ? projectSysTarget.current : undefined}
          document={active}
          snippets={snippets}
          onSnippets={setSnippets}
          onProjects={setProjects}
          onClose={() => { setDialog(""); setCatalogTemplate(""); }}
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
  expanded,
  setExpanded,
  selected,
  onSelect,
  onOpen,
  onOpenSource,
  onEdit,
  onScript,
  onContext,
  allowMenuActions,
}: {
  nodes: CommandNode[];
  expanded: Set<string>;
  setExpanded: (update: (previous: Set<string>) => Set<string>) => void;
  selected: string;
  onSelect: (id: string) => void;
  onOpen: (node: CommandNode) => void;
  onOpenSource: (node: CommandNode) => void;
  onEdit: (node: CommandNode) => void;
  onScript: (node: CommandNode) => void;
  onContext: (node: CommandNode, x: number, y: number) => void;
  allowMenuActions: boolean;
}) {
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
                onClick={(event) => {
                  onSelect(node.entry.id);
                  if (node.children.length)
                    setExpanded((old) => {
                      const next = new Set(old);
                      if (next.has(node.entry.id)) next.delete(node.entry.id);
                      else next.add(node.entry.id);
                      return next;
                    });
                  else if (event.detail === 0 && (node.entry.link || node.entry.sysId))
                    onOpen(node);
                }}
                onDoubleClick={() => {
                  if (!node.children.length && (node.entry.link || node.entry.sysId)) onOpen(node);
                }}
                onKeyDown={(event) => {
                  if (allowMenuActions && event.key === "F3") {
                    event.preventDefault();
                    onEdit(node);
                  } else if (allowMenuActions && event.key === "F12") {
                    event.preventDefault();
                    event.stopPropagation();
                    onScript(node);
                  }
                }}
                onContextMenu={(event) => {
                  if (!allowMenuActions) return;
                  event.preventDefault();
                  onSelect(node.entry.id);
                  onContext(node, event.clientX, event.clientY);
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
                  expanded={expanded}
                  setExpanded={setExpanded}
                  selected={selected}
                  onSelect={onSelect}
                  onOpen={onOpen}
                  onOpenSource={onOpenSource}
                  onEdit={onEdit}
                  onScript={onScript}
                  onContext={onContext}
                  allowMenuActions={allowMenuActions}
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
