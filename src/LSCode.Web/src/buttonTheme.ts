import type { CSSProperties } from "react";

export const buttonRoles = ["primary", "secondary", "ghost", "destructive", "icon"] as const;
export const colorParts = ["background", "foreground", "border"] as const;
export type ButtonRole = typeof buttonRoles[number];
export type ColorPart = typeof colorParts[number];
export type ButtonPalette = Partial<Record<ButtonRole, Partial<Record<ColorPart, string>>>>;
export type ButtonColors = Record<string, ButtonPalette>;
export const isButtonColor = (value?: string | null): value is string => /^#[0-9a-f]{6}$/i.test(value ?? "");

export function buttonThemeStyle(palette?: ButtonPalette, resetInherited = false): CSSProperties {
  const style: Record<string, string> = {};
  for (const role of buttonRoles) for (const part of colorParts) {
    const color = palette?.[role]?.[part];
    if (isButtonColor(color)) style[`--button-${role}-${part}`] = color;
    else if (resetInherited) style[`--button-${role}-${part}`] = "initial";
    if (part === "background") {
      if (isButtonColor(color)) style[`--button-${role}-hover`] = `color-mix(in srgb, ${color}, var(--foreground) 6%)`;
      else if (resetInherited) style[`--button-${role}-hover`] = "initial";
    }
  }
  return style as CSSProperties;
}

export function applyButtonTheme(palette?: ButtonPalette) {
  const root = document.documentElement;
  const colors = buttonThemeStyle(palette) as Record<string, string>;
  for (const role of buttonRoles) for (const part of [...colorParts, "hover"]) {
    const name = `--button-${role}-${part}`;
    if (colors[name]) root.style.setProperty(name, colors[name]);
    else root.style.removeProperty(name);
  }
}

export function luminance(hex: string) {
  const channels = [1, 3, 5].map(index => {
    const value = parseInt(hex.slice(index, index + 2), 16) / 255;
    return value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4;
  });
  return .2126 * channels[0] + .7152 * channels[1] + .0722 * channels[2];
}
