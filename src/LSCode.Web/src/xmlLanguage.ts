import * as monaco from "monaco-editor";

export const xmlLanguage = "lscode-xml";
monaco.languages.register({ id: xmlLanguage });
monaco.languages.setLanguageConfiguration(xmlLanguage, {
  comments: { blockComment: ["<!--", "-->"] },
  brackets: [["<", ">"]],
  autoClosingPairs: [{ open: "<", close: ">" }, { open: '"', close: '"' }, { open: "'", close: "'" }],
  surroundingPairs: [{ open: "<", close: ">" }, { open: '"', close: '"' }, { open: "'", close: "'" }],
  onEnterRules: [{
    beforeText: /<[\w:.-]+(?:\s+[^<>]*)?(?<!\/)>[^<]*$/,
    afterText: /^<\/[\w:.-]+\s*>$/,
    action: { indentAction: monaco.languages.IndentAction.IndentOutdent },
  }, {
    beforeText: /<[\w:.-]+(?:\s+[^<>]*)?(?<!\/)>[^<]*$/,
    action: { indentAction: monaco.languages.IndentAction.Indent },
  }],
});
monaco.languages.setMonarchTokensProvider(xmlLanguage, {
  tokenPostfix: ".xml",
  ignoreCase: true,
  tokenizer: {
    root: [
      [/<!--/, { token: "comment", next: "@comment" }],
      [/<!\[CDATA\[/, { token: "delimiter.cdata", next: "@cdata", nextEmbedded: "sql" }],
      [/(<)(script)(?=\s|\/?>)/, ["delimiter", { token: "tag", next: "@scriptTag" }]],
      [/(<)(sql|query)(?=\s|\/?>)/, ["delimiter", { token: "tag", next: "@sqlTag" }]],
      [/(<\/)([\w:.-]+)(\s*>)/, ["delimiter", "tag", "delimiter"]],
      [/(<[?!])([\w:.-]+)/, ["delimiter", { token: "metatag", next: "@tag" }]],
      [/(<)([\w:.-]+)/, ["delimiter", { token: "tag", next: "@tag" }]],
      [/&(?:[\w:.-]+|#[\w]+);/, "string.escape"],
      [/[^<&]+/, ""],
    ],
    attributes: [
      [/([\w:.-]+)(\s*=\s*)/, ["attribute.name", "delimiter"]],
      [/"/, { token: "attribute.value", next: "@doubleQuote" }],
      [/'/, { token: "attribute.value", next: "@singleQuote" }],
      [/[\w:.-]+/, "attribute.name"],
      [/\s+/, ""],
    ],
    doubleQuote: [[/[^"]+/, "attribute.value"], [/"/, { token: "attribute.value", next: "@pop" }]],
    singleQuote: [[/[^']+/, "attribute.value"], [/'/, { token: "attribute.value", next: "@pop" }]],
    tag: [[/\??\/?>/, { token: "delimiter", next: "@pop" }], { include: "@attributes" }],
    scriptTag: [
      [/\/>/, { token: "delimiter", next: "@pop" }],
      [/>/, { token: "delimiter", switchTo: "@scriptBody" }],
      { include: "@attributes" },
    ],
    scriptBody: [
      [/(<\/)(script)(\s*>)/, ["delimiter", "tag", { token: "delimiter", next: "@pop" }]],
      [/<!\[CDATA\[/, { token: "delimiter.cdata", next: "@cdata", nextEmbedded: "javascript" }],
      { include: "@root" },
    ],
    sqlTag: [
      [/\/>/, { token: "delimiter", next: "@pop" }],
      [/>/, { token: "delimiter", switchTo: "@sqlBody" }],
      { include: "@attributes" },
    ],
    sqlBody: [
      [/(<\/)(sql|query)(\s*>)/, ["delimiter", "tag", { token: "delimiter", next: "@pop" }]],
      [/<!\[CDATA\[/, { token: "delimiter.cdata", next: "@cdata", nextEmbedded: "sql" }],
      [/\s+/, ""],
      [/./, { token: "@rematch", next: "@inlineSql", nextEmbedded: "sql" }],
    ],
    inlineSql: [[/<\/(?:sql|query)\s*>/, { token: "@rematch", next: "@pop", nextEmbedded: "@pop" }]],
    cdata: [[/\]\]>/, { token: "delimiter.cdata", next: "@pop", nextEmbedded: "@pop" }]],
    comment: [[/-->/, { token: "comment", next: "@pop" }], [/[^-]+|-/, "comment"]],
  },
});
