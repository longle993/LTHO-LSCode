import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { readFile } from "node:fs/promises";
import { dirname } from "node:path";

const monacoInlineController = /[/\\]monaco-editor[/\\].*[/\\]inlineCompletionsController\.js$/;
const inlineAccessibilityWait = /await timeout\(50, cancelOnDispose\(store\)\);\s+await waitForState\(this\._suggestWidgetSelectedItem, isUndefined, \(\) => false, cancelOnDispose\(store\)\);/;

function patchMonacoInlineAccessibility(source: string) {
  // Monaco 0.52.2 leaves these promises unhandled when the suggestion changes or closes.
  if (!inlineAccessibilityWait.test(source)) return source;
  return "import { isCancellationError } from '../../../../../base/common/errors.js';\n" +
    source.replace(inlineAccessibilityWait, (wait) => `try {
      ${wait}
    } catch (error) {
      if (isCancellationError(error)) return;
      throw error;
    }`);
}

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    {
      name: "lscode-monaco-inline-accessibility",
      enforce: "pre",
      transform(source, id) {
        if (monacoInlineController.test(id)) return patchMonacoInlineAccessibility(source);
      },
    },
  ],
  optimizeDeps: {
    esbuildOptions: {
      plugins: [{
        name: "lscode-monaco-inline-accessibility",
        setup(build) {
          build.onLoad({ filter: monacoInlineController }, async ({ path }) => ({
            contents: patchMonacoInlineAccessibility(await readFile(path, "utf8")),
            resolveDir: dirname(path),
            loader: "js",
          }));
        },
      }],
    },
  },
  base: "./",
  build: { chunkSizeWarningLimit: 3000 },
});
