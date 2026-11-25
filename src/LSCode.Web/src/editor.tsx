import { useEffect, useRef } from "react";
import * as monaco from "monaco-editor";
import EditorWorker from "monaco-editor/esm/vs/editor/editor.worker?worker";
import JsonWorker from "monaco-editor/esm/vs/language/json/json.worker?worker";
import TsWorker from "monaco-editor/esm/vs/language/typescript/ts.worker?worker";
import HtmlWorker from "monaco-editor/esm/vs/language/html/html.worker?worker";
import CssWorker from "monaco-editor/esm/vs/language/css/css.worker?worker";
import { request } from "./bridge";
import type { DocumentInfo, Snippet, Target } from "./bridge";
import { xmlLanguage } from "./xmlLanguage";

type XmlProblem = { line: number; column: number; message: string };
export type EditorDiagnostic = XmlProblem & { severity: "error" | "warning" };
const toDiagnostic = (marker: monaco.editor.IMarker): EditorDiagnostic => ({
  line: marker.startLineNumber,
  column: marker.startColumn,
  message: marker.message,
  severity: marker.severity === monaco.MarkerSeverity.Warning ? "warning" : "error",
});

(self as unknown as { MonacoEnvironment: unknown }).MonacoEnvironment = {
  getWorker: (_: string, label: string) =>
    label === "json"
      ? new JsonWorker()
      : label === "typescript" || label === "javascript"
        ? new TsWorker()
        : label === "html"
          ? new HtmlWorker()
          : label === "css" || label === "scss" || label === "less"
            ? new CssWorker()
            : new EditorWorker(),
};
export const language = (name: string) =>
  ({
    sql: "sql",
    xml: "xml",
    f: "xml",
    ent: "xml",
    xaml: "xml",
    config: "xml",
    aspx: "html",
    json: "json",
    js: "javascript",
    ts: "typescript",
    cs: "csharp",
    md: "markdown",
  })[name.split(".").pop()?.toLowerCase() ?? ""] ?? "plaintext";
const editorLanguage = (name: string) => {
  const id = language(name);
  return id === "xml" ? xmlLanguage : id;
};
const viewStates = new Map<string, monaco.editor.ICodeEditorViewState>();
const modelSources = new Map<string, { path: string | null; initialVersion: number; validate: () => void }>();
let userSqlSnippets: Snippet[] = [];
export function setEditorSnippets(snippets: Snippet[]) {
  userSqlSnippets = snippets.filter((snippet) => snippet.key.trim().startsWith("/") && snippet.body.length > 0);
}
export function xmlIncludeBuffers() {
  return Object.fromEntries(monaco.editor.getModels().flatMap((model) => {
    const source = modelSources.get(model.uri.toString());
    return source?.path && model.getVersionId() !== source.initialVersion && model.getValueLength() <= 1_000_000
      ? [[source.path, model.getValue()]] : [];
  }));
}
export const modelFor = (d: DocumentInfo) => {
  const existing = monaco.editor.getModel(
    monaco.Uri.parse("lscode://document/" + d.id),
  );
  if (existing) {
    const source = modelSources.get(existing.uri.toString());
    if (source) source.path = d.path;
    return existing;
  }
  const model = monaco.editor.createModel(
    d.text,
    editorLanguage(d.name),
    monaco.Uri.parse("lscode://document/" + d.id),
  );
  model.setEOL(
    d.newline === "CRLF"
      ? monaco.editor.EndOfLineSequence.CRLF
      : monaco.editor.EndOfLineSequence.LF,
  );
  {
    let timer: ReturnType<typeof setTimeout>;
    const validate = () => {
      clearTimeout(timer);
      timer = setTimeout(async () => {
        if (model.isDisposed()) return;
        if (model.getLanguageId() !== xmlLanguage) {
          monaco.editor.setModelMarkers(model, "xml", []);
          return;
        }
        const version = model.getVersionId(),
          text = model.getValue();
        if (text.length > 1000000) {
          monaco.editor.setModelMarkers(model, "xml", []);
          return;
        }
        try {
          const { diagnostic, warnings } = await request<{
            diagnostic: XmlProblem | null;
            warnings: XmlProblem[];
          }>("xml.diagnostics", { text, path: modelSources.get(model.uri.toString())?.path, buffers: xmlIncludeBuffers() });
          if (model.isDisposed() || version !== model.getVersionId() || model.getLanguageId() !== xmlLanguage) return;
          monaco.editor.setModelMarkers(model, "xml", (diagnostic ? [diagnostic] : warnings).map((problem) => ({
            severity: diagnostic ? monaco.MarkerSeverity.Error : monaco.MarkerSeverity.Warning,
            message: problem.message,
            startLineNumber: problem.line,
            endLineNumber: problem.line,
            startColumn: problem.column,
            endColumn: problem.column + 1,
          })));
        } catch {
          /* Native bridge errors are surfaced by the workbench; no fabricated diagnostics. */
        }
      }, 400);
    };
    const changed = model.onDidChangeContent(validate);
    const changedLanguage = model.onDidChangeLanguage(validate);
    modelSources.set(model.uri.toString(), { path: d.path, initialVersion: model.getVersionId(), validate });
    model.onWillDispose(() => {
      modelSources.delete(model.uri.toString());
      clearTimeout(timer);
      changed.dispose();
      changedLanguage.dispose();
    });
    validate();
  }
  return model;
};
export const textOf = (d: DocumentInfo) =>
  monaco.editor.getModel(monaco.Uri.parse("lscode://document/" + d.id))
    ?.getValue(monaco.editor.EndOfLinePreference.TextDefined) ?? d.text;
export function syncSavedText(saved: DocumentInfo, submittedText: string) {
  if (saved.text === submittedText) return;
  const model = monaco.editor.getModel(monaco.Uri.parse("lscode://document/" + saved.id));
  if (model?.getValue() !== submittedText) return;
  model.pushEditOperations(null, [{ range: model.getFullModelRange(), text: saved.text }], () => null);
}
export const disposeModel = (d: DocumentInfo) => {
  const uri = monaco.Uri.parse("lscode://document/" + d.id);
  targetByModel.delete(uri.toString());
  for (const key of viewStates.keys())
    if (key.startsWith(uri.toString() + "|")) viewStates.delete(key);
  monaco.editor.getModel(uri)?.dispose();
};
export function markSqlErrors(
  d: DocumentInfo,
  errors: { line: number; message: string; procedure: string }[],
  startLine = 1,
  startColumn = 1,
) {
  const model = modelFor(d);
  monaco.editor.setModelMarkers(
    model,
    "sql",
    errors
      .filter((e) => e.line > 0 && !e.procedure)
      .map((e) => ({
        severity: monaco.MarkerSeverity.Error,
        message: e.message,
        startLineNumber: Math.min(model.getLineCount(), startLine + e.line - 1),
        endLineNumber: Math.min(model.getLineCount(), startLine + e.line - 1),
        startColumn: e.line === 1 ? startColumn : 1,
        endColumn: 1000,
      })),
  );
}
const targetByModel = new Map<string, Target | undefined>();
type CompletionSnippet = { key: string; body: string; description: string };
let completionSnippets: Promise<CompletionSnippet[]> | undefined;
const sqlCompletionSnippets = () => completionSnippets ??= request<CompletionSnippet[]>("snippet.completions", { language: "sql" });
monaco.languages.registerCompletionItemProvider(xmlLanguage, {
  triggerCharacters: ["@"],
  async provideCompletionItems(model, position) {
    const line = model.getLineContent(position.lineNumber);
    const before = line.slice(0, position.column - 1);
    const prefix = before.match(/(?<![\w@#$])@@[\w$]*$/);
    if (!prefix) return;
    const start = prefix.index!;
    const text = model.getValueInRange({
      startLineNumber: 1, startColumn: 1,
      endLineNumber: position.lineNumber, endColumn: position.column,
    });
    const token = monaco.editor.tokenize(text, xmlLanguage).at(-1)
      ?.reverse().find((candidate) => candidate.offset <= start);
    if (token?.language !== "sql" || !/^(identifier|predefined)\.sql$/.test(token.type)) return;
    const suffix = line.slice(position.column - 1).match(/^[\w$]*/)?.[0] ?? "";
    const snippets = await sqlCompletionSnippets();
    return {
      suggestions: snippets.filter((snippet) => snippet.key.startsWith("@@") && !snippet.body.includes("{{")).map((snippet) => ({
        label: snippet.key,
        detail: snippet.description || "Biến session FAST",
        insertText: snippet.body,
        kind: monaco.languages.CompletionItemKind.Variable,
        range: {
          startLineNumber: position.lineNumber, endLineNumber: position.lineNumber,
          startColumn: start + 1, endColumn: position.column + suffix.length,
        },
      })),
    };
  },
});
monaco.languages.registerCompletionItemProvider("sql", {
  triggerCharacters: [".", "/", "@", "#"],
  provideCompletionItems: async (model, position, _, token) => {
    const beforeLine = model.getLineContent(position.lineNumber).slice(0, position.column - 1);
    const quickKey = beforeLine.match(/(?:^|[\s(,;])(?<key>\/[^\s(,;]*)$/u)?.groups?.key;
    if (quickKey) {
      const startColumn = position.column - quickKey.length;
      const search = quickKey.toLowerCase();
      return {
        suggestions: userSqlSnippets
          .filter((snippet) => snippet.key.toLowerCase().startsWith(search))
          .map((snippet, index) => ({
            label: snippet.key,
            filterText: snippet.key,
            sortText: String(index).padStart(4, "0"),
            insertText: userSqlSnippet(snippet),
            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            detail: snippet.description || snippet.category || "Snippet người dùng",
            documentation: { value: `**${snippet.key}**\n\n${snippet.body}` },
            kind: monaco.languages.CompletionItemKind.Snippet,
            range: {
              startLineNumber: position.lineNumber,
              endLineNumber: position.lineNumber,
              startColumn,
              endColumn: position.column,
            },
          })),
      };
    }
    const target = targetByModel.get(model.uri.toString());
    const word = model.getWordUntilPosition(position);
    const localPrefix = beforeLine.match(/(?<![\p{L}\p{N}_@$#])(?:@{1,2}|#{1,2})[\p{L}\p{N}_$]*$/u)?.[0];
    const localSuffix = localPrefix
      ? model.getLineContent(position.lineNumber).slice(position.column - 1).match(/^[\p{L}\p{N}_$]*/u)?.[0] ?? ""
      : "";
    const range = {
      startLineNumber: position.lineNumber,
      endLineNumber: position.lineNumber,
      startColumn: localPrefix ? position.column - localPrefix.length : word.startColumn,
      endColumn: localPrefix ? position.column + localSuffix.length : word.endColumn,
    };
    try {
      const values = await request<
        {
          text: string;
          insertText: string;
          description: string;
          isSnippet: boolean;
        }[]
      >("sql.complete", {
        targetId: localPrefix ? undefined : target?.id,
        text: model.getValue(),
        beforeCaret: model.getValueInRange({
          startLineNumber: 1,
          startColumn: 1,
          endLineNumber: position.lineNumber,
          endColumn: position.column,
        }),
      });
      if (
        token.isCancellationRequested ||
        model.isDisposed() ||
        targetByModel.get(model.uri.toString())?.id !== target?.id
      )
        return { suggestions: [] };
      return {
        suggestions: values.map((x) => ({
          label: x.text,
          insertText: x.isSnippet ? sqlSnippet(x.insertText) : x.insertText,
          insertTextRules: x.isSnippet
            ? monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet
            : undefined,
          detail: x.description,
          kind: x.isSnippet
            ? monaco.languages.CompletionItemKind.Snippet
            : monaco.languages.CompletionItemKind.Field,
          range,
        })),
      };
    } catch {
      return { suggestions: [] };
    }
  },
});
function sqlSnippet(body: string) {
  let n = 0;
  const escapeLiteral = (text: string) => text.replace(/[\\$}]/g, character => "\\" + character);
  return body.replace(/\{\{(?:[-|]|\d+)(?:-([^}]*))?\}\}|[\\$}]/g, (match, label) =>
    match.length === 1 ? escapeLiteral(match) : "${" + ++n + ":" + escapeLiteral(label ?? "") + "}");
}
function userSqlSnippet(snippet: Snippet) {
  const position = Math.max(0, Math.min(snippet.position, snippet.body.length));
  const marker = "\u0000LSCODE_CURSOR\u0000";
  return sqlSnippet(snippet.body.slice(0, position) + marker + snippet.body.slice(position))
    .replace(marker, "$0");
}
for (const lang of ["javascript", xmlLanguage, "html"])
  monaco.languages.registerCompletionItemProvider(lang, {
    triggerCharacters: [".", "'", '"'],
    provideCompletionItems: async (model, position, _, token) => {
      const word = model.getWordUntilPosition(position);
      const range = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        endColumn: word.endColumn,
      };
      try {
        const values = await request<
          { text: string; insertText: string; description: string; isSnippet: boolean }[]
        >("javascript.complete", {
          language: lang === xmlLanguage ? "xml" : lang,
          text: model.getValue(),
          beforeCaret: model.getValueInRange({
            startLineNumber: 1,
            startColumn: 1,
            endLineNumber: position.lineNumber,
            endColumn: position.column,
          }),
        });
        if (token.isCancellationRequested) return { suggestions: [] };
        return {
          suggestions: values.map((x) => ({
            label: x.text,
            insertText: x.insertText,
            detail: x.description,
            kind: x.isSnippet ? monaco.languages.CompletionItemKind.Snippet : monaco.languages.CompletionItemKind.Field,
            range,
          })),
        };
      } catch {
        return { suggestions: [] };
      }
    },
  });
monaco.languages.registerHoverProvider(xmlLanguage, {
  async provideHover(model, position, token) {
    const hint = await request<{
      field: string;
      width: number;
      columns: number[];
    } | null>("xml.viewWidth", {
      text: model.getValue(),
      offset: model.getOffsetAt(position),
    }).catch(() => null);
    if (!hint || token.isCancellationRequested || model.isDisposed())
      return null;
    return {
      contents: [
        {
          value:
            hint.field +
            " · **" +
            hint.width +
            " px** (" +
            hint.columns.join(" + ") +
            ")",
        },
      ],
    };
  },
});
export type EditorHandle = monaco.editor.IStandaloneCodeEditor;
export function CodeEditor({
  document,
  theme,
  target,
  fontSize = 14,
  onChange,
  onMount,
  onCursor,
  readOnly = false,
  onDiagnostics,
  viewId = "primary",
}: {
  document: DocumentInfo;
  theme: string;
  fontSize?: number;
  target?: Target;
  readOnly?: boolean;
  onDiagnostics?: (
    diagnostics: EditorDiagnostic[],
  ) => void;
  viewId?: string;
  onChange: () => void;
  onMount: (e: EditorHandle) => void;
  onCursor: (s: string) => void;
}) {
  const host = useRef<HTMLDivElement>(null),
    instance = useRef<EditorHandle>();
  const callbacks = useRef({ onChange, onMount, onCursor, onDiagnostics });
  callbacks.current = { onChange, onMount, onCursor, onDiagnostics };
  useEffect(() => {
    const editor = monaco.editor.create(host.current!, {
      readOnly,
      automaticLayout: true,
      fontFamily: "Cascadia Code, Consolas, monospace",
      fontSize,
      lineHeight: Math.round(fontSize * 23 / 14),
      padding: { top: 18 },
      minimap: { enabled: false },
      scrollBeyondLastLine: false,
      smoothScrolling: true,
      mouseWheelZoom: true,
      renderLineHighlight: "gutter",
      // Monaco 0.52 word-highlighter leaves a rejected Delayer on model disposal.
      // Keep textual selection highlighting while independent panes switch models.
      occurrencesHighlight: "off",
      selectionHighlight: true,
      tabSize: 4,
      wordWrap: "off",
      fixedOverflowWidgets: true,
      accessibilitySupport: "auto",
    });
    instance.current = editor;
    const change = editor.onDidChangeModelContent(() =>
      callbacks.current.onChange(),
    );
    let hintGeneration = 0;
    const cursor = editor.onDidChangeCursorPosition(async (e) => {
      const generation = ++hintGeneration,
        model = editor.getModel(),
        basic = `Dòng ${e.position.lineNumber}, cột ${e.position.column}`;
      callbacks.current.onCursor(basic);
      if (model?.getLanguageId() !== xmlLanguage) return;
      const hint = await request<{
        field: string;
        width: number;
        columns: number[];
      } | null>("xml.viewWidth", {
        text: model.getValue(),
        offset: model.getOffsetAt(e.position),
      }).catch(() => null);
      if (generation === hintGeneration && hint)
        callbacks.current.onCursor(
          basic +
            " · " +
            hint.field +
            " = " +
            hint.width +
            " px (" +
            hint.columns.join(" + ") +
            ")",
        );
    });
    const focus = editor.onDidFocusEditorText(() =>
      callbacks.current.onMount(editor),
    );
    const markers = monaco.editor.onDidChangeMarkers((uris) => {
      const model = editor.getModel();
      if (model && uris.some((uri) => uri.toString() === model.uri.toString()))
        callbacks.current.onDiagnostics?.(
          monaco.editor.getModelMarkers({ resource: model.uri }).map(toDiagnostic),
        );
    });
    const keys = editor.onKeyDown((e) => {
      if (e.keyCode !== monaco.KeyCode.Tab) return;
      const model = editor.getModel(),
        selection = editor.getSelection();
      if (!model || !selection) return;
      const matches = model.findMatches(
        "\\{\\{[^{}]*\\}\\}",
        false,
        true,
        false,
        null,
        true,
      );
      if (!matches.length) return;
      const caret = selection.getStartPosition();
      const next = e.shiftKey
        ? ([...matches]
            .reverse()
            .find((m) => m.range.getStartPosition().isBefore(caret)) ??
          matches.at(-1)!)
        : (matches.find((m) => caret.isBefore(m.range.getStartPosition())) ??
          matches[0]);
      e.preventDefault();
      e.stopPropagation();
      editor.setSelection(next.range);
      editor.revealRangeInCenterIfOutsideViewport(next.range);
    });
    return () => {
      change.dispose();
      cursor.dispose();
      focus.dispose();
      markers.dispose();
      keys.dispose();
      const model = editor.getModel(),
        state = editor.saveViewState();
      if (model && state)
        viewStates.set(model.uri.toString() + "|" + viewId, state);
      editor.dispose();
    };
  }, []);
  useEffect(() => {
    const model = modelFor(document);
    instance.current!.updateOptions({
      quickSuggestions: { other: true, comments: false, strings: model.getLanguageId() === xmlLanguage },
    });
    targetByModel.set(model.uri.toString(), target);
    const previous = instance.current!.getModel(),
      previousState = instance.current!.saveViewState();
    if (previous && previousState)
      viewStates.set(previous.uri.toString() + "|" + viewId, previousState);
    instance.current!.setModel(model);
    const restored = viewStates.get(model.uri.toString() + "|" + viewId);
    if (restored) instance.current!.restoreViewState(restored);
    callbacks.current.onDiagnostics?.(
      monaco.editor.getModelMarkers({ resource: model.uri }).map(toDiagnostic),
    );
    monaco.editor.setModelLanguage(model, editorLanguage(document.name));
    if (previous !== model) modelSources.get(model.uri.toString())?.validate();
    callbacks.current.onMount(instance.current!);
  }, [document.id, document.name, document.path, target?.id]);
  useEffect(() => {
    instance.current?.updateOptions({ fontSize, lineHeight: Math.round(fontSize * 23 / 14) });
  }, [fontSize]);
  useEffect(() => {
    const base = theme === "High Contrast" ? "hc-black" : theme === "Dark" ? "vs-dark" : "vs";
    monaco.editor.defineTheme("lscode-" + base, {
      base, inherit: true,
      rules: [{ token: "delimiter.cdata.xml", foreground: base === "vs" ? "18683C" : base === "hc-black" ? "BFFF00" : "63D990", fontStyle: "bold" }],
      colors: {},
    });
    monaco.editor.setTheme("lscode-" + base);
  }, [theme]);
  return (
    <div ref={host} className="code-editor" aria-label="Trình soạn thảo mã" />
  );
}
