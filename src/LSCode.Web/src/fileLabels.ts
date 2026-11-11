type FileTab = { id: string; name: string; path: string | null };

export function pathIsWithin(path: string, folder: string): boolean {
  const normalizedPath = path.replaceAll("\\", "/").toLowerCase();
  const normalizedFolder = folder.replaceAll("\\", "/").replace(/\/+$/, "").toLowerCase();
  return normalizedPath === normalizedFolder || normalizedPath.startsWith(normalizedFolder + "/");
}

export function rebasePath(path: string, source: string, destination: string): string {
  const oldRoot = source.replace(/[\\/]+$/, "");
  return pathIsWithin(path, oldRoot) ? destination.replace(/[\\/]+$/, "") + path.slice(oldRoot.length) : path;
}

export function tabFolderLabel(tab: FileTab, peers: FileTab[]): string {
  if (peers.length < 2) return "";
  if (!tab.path) return "Chưa lưu · " + (peers.indexOf(tab) + 1);
  const folders = (path: string) => path.replaceAll("\\", "/").split("/").slice(0, -1);
  const parts = folders(tab.path);
  for (let length = 1; length <= parts.length; length++) {
    const suffix = parts.slice(-length).join("/");
    if (peers.every((other) => other.id === tab.id || !other.path ||
      folders(other.path).slice(-length).join("/").toLowerCase() !== suffix.toLowerCase()))
      return suffix;
  }
  return parts.join("/");
}
