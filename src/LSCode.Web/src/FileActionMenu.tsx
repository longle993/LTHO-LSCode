import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import { Button } from "./ui";

export type FileMenuItem = { name: string; path: string; directory: boolean };
export type FileMenuState = { item: FileMenuItem; x: number; y: number };

export function FileActionMenu({
  menu,
  onClose,
  onCopy,
  onReveal,
  onDuplicate,
  onRenameRequest,
  restoreFocus,
  inline = false,
}: {
  menu?: FileMenuState;
  onClose: () => void;
  onCopy: (text: string) => void;
  onReveal: (path: string) => void;
  onDuplicate?: (path: string) => void;
  onRenameRequest?: (item: FileMenuItem) => void;
  restoreFocus?: () => void;
  inline?: boolean;
}) {
  const menuElement = useRef<HTMLDivElement>(null);
  const closeMenu = () => {
    onClose();
    restoreFocus?.();
  };
  useEffect(() => {
    if (!menu) return;
    const dismiss = (event: PointerEvent) => {
      if (!menuElement.current?.contains(event.target as Node)) closeMenu();
    };
    document.addEventListener("pointerdown", dismiss);
    menuElement.current?.querySelector<HTMLButtonElement>("button")?.focus();
    return () => document.removeEventListener("pointerdown", dismiss);
  }, [menu]);
  if (!menu) return null;
  const content = <div
      ref={menuElement}
      className="table-action-menu explorer-action-menu"
      role="menu"
      aria-label="Thao tác file và thư mục"
      style={{ left: menu.x, top: menu.y }}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          closeMenu();
        }
        if (event.key === "Tab") closeMenu();
        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
          event.preventDefault();
          const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>(
            ".explorer-action-menu[role=menu] button[role=menuitem]",
          ));
          const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
          buttons[(index + (event.key === "ArrowDown" ? 1 : -1) + buttons.length) % buttons.length]?.focus();
        }
      }}
    >
      <Button type="button" role="menuitem" variant="ghost" onClick={() => { closeMenu(); onCopy(menu.item.name); }}>Sao chép tên <kbd>Ctrl+C</kbd></Button>
      <Button type="button" role="menuitem" variant="ghost" onClick={() => { closeMenu(); onCopy(menu.item.path); }}>Sao chép đường dẫn</Button>
      {onRenameRequest && <Button type="button" role="menuitem" variant="ghost" onClick={() => { closeMenu(); onRenameRequest(menu.item); }}>Đổi tên <kbd>F2</kbd></Button>}
      <Button type="button" role="menuitem" variant="ghost" onClick={() => {
        closeMenu();
        onReveal(menu.item.path);
      }}>{menu.item.directory ? "Mở thư mục" : "Mở thư mục chứa file"}</Button>
      {!menu.item.directory && onDuplicate && <Button type="button" role="menuitem" variant="ghost" onClick={() => { closeMenu(); onDuplicate(menu.item.path); }}>Nhân bản file</Button>}
    </div>;
  return inline ? content : createPortal(content, document.body);
}
