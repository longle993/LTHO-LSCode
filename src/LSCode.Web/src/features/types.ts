import type {
  DocumentInfo,
  Settings,
  Target,
  Project,
  Snippet,
  Table,
} from "../bridge";
export type ToolProps = {
  kind: string;
  target?: Target;
  document?: DocumentInfo;
  selectedText?: string;
  snippets: Snippet[];
  onSnippets: (s: Snippet[]) => void;
  onProjects: (p: Project[]) => void;
  onClose: () => void;
  onDirtyChange?: (dirty: boolean) => void;
  getText: () => string;
  onText: (text: string, name: string) => void;
  onInsert: (text: string, position?: number) => void;
  onOpen: (path: string) => void;
};
