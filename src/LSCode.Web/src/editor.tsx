import { useEffect, useRef } from "react";
import * as monaco from "monaco-editor";
import EditorWorker from "monaco-editor/esm/vs/editor/editor.worker?worker";
import JsonWorker from "monaco-editor/esm/vs/language/json/json.worker?worker";
import TsWorker from "monaco-editor/esm/vs/language/typescript/ts.worker?worker";
import HtmlWorker from "monaco-editor/esm/vs/language/html/html.worker?worker";
import CssWorker from "monaco-editor/esm/vs/language/css/css.worker?worker";
import { request } from "./bridge";
import type { DocumentInfo, Target } from "./bridge";

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
const viewStates = new Map<string, monaco.editor.ICodeEditorViewState>();
export const modelFor = (d: DocumentInfo) => {
  const existing = monaco.editor.getModel(
    monaco.Uri.parse("lscode://document/" + d.id),
  );
  if (existing) return existing;
  const model = monaco.editor.createModel(
    d.text,
    language(d.name),
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
        if (model.getLanguageId() !== "xml") {
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
          const problem = await request<{
            line: number;
            column: number;
            message: string;
          } | null>("xml.validate", { text });
          if (!model.isDisposed() && version === model.getVersionId())
            monaco.editor.setModelMarkers(
              model,
              "xml",
              problem
                ? [
                    {
                      severity: monaco.MarkerSeverity.Error,
                      message: problem.message,
                      startLineNumber: problem.line,
                      endLineNumber: problem.line,
                      startColumn: problem.column,
                      endColumn: problem.column + 1,
                    },
                  ]
                : [],
            );
        } catch {
          /* Native bridge errors are surfaced by the workbench; no fabricated diagnostics. */
        }
      }, 400);
    };
    const changed = model.onDidChangeContent(validate);
    const changedLanguage = model.onDidChangeLanguage(validate);
    model.onWillDispose(() => {
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
monaco.languages.registerCompletionItemProvider("sql", {
  triggerCharacters: ["."],
  provideCompletionItems: async (model, position, _, token) => {
    const target = targetByModel.get(model.uri.toString());
    const word = model.getWordUntilPosition(position);
    const range = {
      startLineNumber: position.lineNumber,
      endLineNumber: position.lineNumber,
      startColumn: word.startColumn,
      endColumn: word.endColumn,
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
        targetId: target?.id,
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
  return body.replace(
    /\{\{(?:[-|]|\d+)(?:-([^}]*))?\}\}/g,
    (_, label) => "${" + ++n + ":" + (label ?? "") + "}",
  );
}
for (const lang of ["javascript", "xml", "html"])
  monaco.languages.registerCompletionItemProvider(lang, {
    triggerCharacters: ["."],
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
          { text: string; insertText: string; description: string }[]
        >("javascript.complete", {
          language: lang,
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
            kind: monaco.languages.CompletionItemKind.Snippet,
            range,
          })),
        };
      } catch {
        return { suggestions: [] };
      }
    },
  });
monaco.languages.registerHoverProvider("xml", {
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
  onChange,
  onMount,
  onCursor,
  readOnly = false,
  onDiagnostics,
  viewId = "primary",
}: {
  document: DocumentInfo;
  theme: string;
  target?: Target;
  readOnly?: boolean;
  onDiagnostics?: (
    diagnostics: { line: number; column: number; message: string }[],
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
      fontSize: 14,
      lineHeight: 23,
      padding: { top: 18 },
      minimap: { enabled: false },
      scrollBeyondLastLine: false,
      smoothScrolling: true,
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
      if (model?.getLanguageId() !== "xml") return;
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
    const markers = monaco.editor.onDidChangeMarkers(() => {
      const model = editor.getModel();
      if (model)
        callbacks.current.onDiagnostics?.(
          monaco.editor.getModelMarkers({ resource: model.uri }).map((m) => ({
            line: m.startLineNumber,
            column: m.startColumn,
            message: m.message,
          })),
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
    targetByModel.set(model.uri.toString(), target);
    const previous = instance.current!.getModel(),
      previousState = instance.current!.saveViewState();
    if (previous && previousState)
      viewStates.set(previous.uri.toString() + "|" + viewId, previousState);
    instance.current!.setModel(model);
    const restored = viewStates.get(model.uri.toString() + "|" + viewId);
    if (restored) instance.current!.restoreViewState(restored);
    callbacks.current.onDiagnostics?.(
      monaco.editor.getModelMarkers({ resource: model.uri }).map((m) => ({
        line: m.startLineNumber,
        column: m.startColumn,
        message: m.message,
      })),
    );
    monaco.editor.setModelLanguage(model, language(document.name));
    callbacks.current.onMount(instance.current!);
  }, [document.id, document.name, target?.id]);
  useEffect(() => {
    monaco.editor.setTheme(
      theme === "High Contrast"
        ? "hc-black"
        : theme === "Dark"
          ? "vs-dark"
          : "vs",
    );
  }, [theme]);
  return (
    <div ref={host} className="code-editor" aria-label="Trình soạn thảo mã" />
  );
}
