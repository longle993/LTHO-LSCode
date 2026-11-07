using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Windows.Input;
using System.Xml;
using LSCode.Core;

namespace LSCode.Shell;

public sealed class EditorTab
{
    public string? Path { get; set; }
    public bool ShowParentFolder { get; set; }
    public string Name { get; set; } = "Untitled.sql";
    public FileSnapshot? Snapshot { get; set; }
    private LSCode.Shell.Services.SqlTarget? sqlTarget;
    public LSCode.Shell.Services.SqlTarget? SqlTarget { get => sqlTarget; set { if (sqlTarget != value) { sqlTarget = value; InvalidateCompletionCache(); completionWindow?.Close(); } } }
    public LSCode.Core.LegacyProject? Project { get; set; }
    public TextBlock? TargetLabel { get; set; }
    public Task TargetReady { get; set; } = Task.CompletedTask;
    public Func<Task<IReadOnlyList<SqlSymbol>>>? CompletionLoader { get; set; }
    public event Action<string>? CompletionError;
    private CompletionWindow? completionWindow;
    private bool loadingCompletion, disposed;
    private IReadOnlyList<SqlSymbol>? cachedSymbols;
    private static readonly Lazy<IReadOnlyList<CompletionSnippet>> jsSnippets = new(() => LoadSnippets("jssnippet.xml"));
    private static readonly Lazy<IReadOnlyList<CompletionSnippet>> sqlSnippets = new(() => LoadSnippets("sqlsnippet.xml"));
    private static IReadOnlyList<CompletionSnippet> LoadSnippets(string file) => CompletionSnippets.Read(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", file)));
    private bool ScriptContext(int offset) => Language is ".xml" or ".xaml" or ".config" or ".html" or ".aspx" or ".f" or ".ent" && CompletionSnippets.IsInScript(Buffer.Text, offset);
    public TextDocument Buffer { get; } = new();
    public TextEditor Primary { get; }
    public TextEditor? Secondary { get; private set; }
    public TextEditor ActiveEditor { get; private set; }
    public TabItem Tab { get; } = new();
    public Grid Layout { get; } = new();
    public bool Dirty => Buffer.Text != (Snapshot?.Text ?? "");
    public long Version { get; private set; }
    public string Language => System.IO.Path.GetExtension(Path ?? Name).ToLowerInvariant();
    public event Action? Changed;
    public event Action? CaretChanged;
    private readonly List<FoldingManager> foldingManagers = [];

    public EditorTab(string name, string text)
    {
        Name = name; Buffer.Text = text;
        Primary = CreateEditor(); ActiveEditor = Primary;
        Layout.Children.Add(Primary); Tab.Content = Layout; Tab.Tag = this;
        Buffer.Changed += (_, _) => { Version++; Changed?.Invoke(); };
    }
    private static FontFamily EditorFont()
    {
        // Composite font metrics apply to AvalonEdit's measured text lines, including
        // wrapping, caret hit-testing, selection and the shared split document.
        var font = new FontFamily { LineSpacing = 1.65, Baseline = 1.25 };
        font.FamilyMaps.Add(new FontFamilyMap { Target = "Cascadia Mono, Consolas" });
        return font;
    }

    private TextEditor CreateEditor()
    {
        var editor = new TextEditor
        {
            Document = Buffer, ShowLineNumbers = true, FontFamily = EditorFont(),
            FontSize = 13, Padding = new Thickness(8, 10, 8, 10),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        editor.Options.IndentationSize = 4;
        editor.Options.ConvertTabsToSpaces = false;
        editor.SetResourceReference(Control.BackgroundProperty, "Surface");
        editor.SetResourceReference(Control.ForegroundProperty, "Text");
        editor.LineNumbersForeground = (Brush)Application.Current.FindResource("Muted");
        var definition = Language switch { ".xml" or ".xaml" or ".config" => "XML", ".cs" => "C#", ".js" or ".ts" => "JavaScript", ".json" => "JavaScript", ".sql" => "TSQL", ".html" => "HTML", ".css" => "CSS", _ => "" };
        editor.SyntaxHighlighting = CreateHighlighting(Language, (string?)Application.Current.Resources["ThemeName"] ?? "Dark");
        SearchPanel.Install(editor);
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Tab || editor.IsReadOnly || completionWindow != null ||
                (Keyboard.Modifiers & ~ModifierKeys.Shift) != ModifierKeys.None) return;
            if (SelectPlaceholder(editor, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) e.Handled = true;
        };
        if (Language is ".xml" or ".xaml" or ".config") foldingManagers.Add(FoldingManager.Install(editor.TextArea));
        editor.TextArea.TextEntered += async (_, e) =>
        {
            if (e.Text.Length != 1) return;
            if (Language != ".sql" && !ScriptContext(editor.CaretOffset)) { completionWindow?.Close(); return; }
            if (char.IsWhiteSpace(e.Text[0]) || e.Text[0] is ';' or ',' or ')' or '(')
            {
                completionWindow?.Close();
                return;
            }
            // Don't auto-trigger if completion is already showing (user can keep typing)

            
            var offset = editor.CaretOffset;
            // Trigger on dot (table.column) or when typing enough letters
            if (e.Text == ".")
            {
                await ShowCompletionAsync(editor);
            }
            else if (char.IsLetter(e.Text[0]) && offset >= 3)
            {
                // Check if we have typed at least 2 identifier chars
                var start = offset;
                while (start > 0 && (char.IsLetterOrDigit(Buffer.GetCharAt(start - 1)) || Buffer.GetCharAt(start - 1) == '_')) start--;
                if (offset - start >= 2) await ShowCompletionAsync(editor);
            }
        };
        editor.GotKeyboardFocus += (_, _) => { ActiveEditor = editor; CaretChanged?.Invoke(); };
        editor.IsVisibleChanged += (_, _) =>
        {
            if (editor.IsVisible)
            {
                editor.SyntaxHighlighting = CreateHighlighting(Language, (string?)Application.Current.Resources["ThemeName"] ?? "Dark");
                editor.LineNumbersForeground = (Brush)Application.Current.FindResource("Muted");
            }
        };
        editor.TextArea.Caret.PositionChanged += (_, _) => CaretChanged?.Invoke();
        return editor;
    }
    public Task CompleteAsync() => ShowCompletionAsync(ActiveEditor, true);
    private async Task ShowCompletionAsync(TextEditor editor, bool explicitTrigger = false)
    {
        if (loadingCompletion || disposed) return;
        var script = ScriptContext(editor.CaretOffset);
        if (Language != ".sql" && !script) { completionWindow?.Close(); return; }
        loadingCompletion = true;
        try { if (!script) await TargetReady; } catch (Exception ex) { CompletionError?.Invoke(ex.Message); }
        var target = SqlTarget;
        IReadOnlyList<SqlSymbol> symbols = cachedSymbols ?? [];
        
        // Load symbols in background only if not cached
        if (!script && CompletionLoader != null)
        {
            try 
            { 
                symbols = await CompletionLoader();
                cachedSymbols = symbols;
            }
            catch (Exception ex) { CompletionError?.Invoke("SQL suggestions: " + ex.Message); }
        }
        loadingCompletion = false;
        
        if (disposed || target != SqlTarget) { cachedSymbols = null; return; }
        if (!editor.IsKeyboardFocusWithin) return;
        var offset = editor.CaretOffset;
        
        if (script && !ScriptContext(offset)) { completionWindow?.Close(); return; }
        var start = offset; while (start > 0 && (char.IsLetterOrDigit(Buffer.GetCharAt(start - 1)) || Buffer.GetCharAt(start - 1) is '_' or '$' or '@')) start--;
        var prefix = Buffer.GetText(start, offset - start);
        
        // Skip if prefix is too short and no explicit trigger
        if (!explicitTrigger && prefix.Length < 2 && offset > 0 && Buffer.GetCharAt(offset - 1) != '.') return;
        
        IReadOnlyList<SqlSuggestion> candidates;
        try { candidates = script ? CompletionSnippets.Get(jsSnippets.Value, Buffer.GetText(0, offset)).ToList() :
            CompletionSnippets.Get(sqlSnippets.Value, Buffer.GetText(0, offset)).Concat(SqlSuggestions.Get(Buffer.GetText(0, offset), symbols, Buffer.Text)).DistinctBy(s => s.Text, StringComparer.OrdinalIgnoreCase).ToList(); }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { candidates = SqlSuggestions.Keywords.Select(k => new SqlSuggestion(k, k, "T-SQL")).ToList(); }
        
        var suggestions = candidates.Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderByDescending(s => s.Text.Equals(prefix, StringComparison.OrdinalIgnoreCase)).ThenBy(s => s.Text, StringComparer.OrdinalIgnoreCase).Take(200).ToList();
        if (suggestions.Count == 0) { completionWindow?.Close(); return; }
        
        completionWindow?.Close();
        var window = new CompletionWindow(editor.TextArea) { StartOffset = start };
        window.SetResourceReference(Control.BackgroundProperty, "Panel"); window.SetResourceReference(Control.ForegroundProperty, "Text");
        completionWindow = window; window.Closed += (_, _) => { if (completionWindow == window) completionWindow = null; };
        foreach (var item in suggestions) window.CompletionList.CompletionData.Add(new KeywordCompletion(item, () => completionWindow?.Close(), script));
        window.Show(); window.CompletionList.SelectItem(prefix);
    }
    
    public void InvalidateCompletionCache() => cachedSymbols = null;
    private bool SelectPlaceholder(TextEditor editor, bool backwards = false)
    {
        if (!ScriptContext(editor.CaretOffset)) return false;
        var placeholders = CompletionSnippets.Placeholders(Buffer.Text).Where(p => ScriptContext(p.Offset)).ToList();
        if (placeholders.Count == 0) return false;
        var offset = editor.SelectionLength > 0 ? editor.SelectionStart : editor.CaretOffset;
        var currentSelected = editor.SelectionLength > 0;
        var candidate = backwards
            ? placeholders.LastOrDefault(p => p.Offset < offset, placeholders[^1])
            : placeholders.FirstOrDefault(p => currentSelected ? p.Offset > offset : p.Offset + p.Length > offset, placeholders[0]);
        editor.Select(candidate.Offset, candidate.Length);
        editor.TextArea.Caret.BringCaretToView();
        return true;
    }
    internal async Task SmokeScriptSnippetsAsync()
    {
        Buffer.Text = "<root><script><![CDATA[g._getI"; Primary.CaretOffset = Buffer.TextLength; Primary.Focus();
        await ShowCompletionAsync(Primary, true);
        if (completionWindow == null || !completionWindow.CompletionList.CompletionData.Any(c => c.Text == "_getItem")) throw new Exception("Missing script member snippet");
        completionWindow.CompletionList.SelectItem("_getItem"); completionWindow.CompletionList.RequestInsertion(EventArgs.Empty);
        if (Buffer.Text != "<root><script><![CDATA[g._getItem({{0-row}}, {{1-col}})" || Primary.SelectedText != "{{0-row}}") throw new Exception("Script snippet expansion or argument selection");
        var tabKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Primary), Environment.TickCount, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        Primary.RaiseEvent(tabKey);
        if (!tabKey.Handled || Primary.SelectedText != "{{1-col}}") throw new Exception("Tab did not select next placeholder");
        if (!SelectPlaceholder(Primary, true) || Primary.SelectedText != "{{0-row}}") throw new Exception("Shift Tab did not select previous placeholder");
        Primary.SelectedText = "2";
        if (!SelectPlaceholder(Primary) || Primary.SelectedText != "{{1-col}}") throw new Exception("Tab navigation after argument replacement");
        Primary.SelectedText = "3";
        if (SelectPlaceholder(Primary)) throw new Exception("Tab must indent normally once placeholders are replaced");
        Buffer.Text = "<root>g._getI"; Primary.CaretOffset = Buffer.TextLength;
        await ShowCompletionAsync(Primary, true);
        if (completionWindow != null) throw new Exception("JS snippet leaked outside script");
    }
    public void UpdateFoldings()
    {
        if (Buffer.TextLength > 1_000_000) return;
        foreach (var manager in foldingManagers)
        {
            try { new XmlFoldingStrategy().UpdateFoldings(manager, Buffer); }
            catch (XmlException) { }
        }
    }
    internal async Task SmokeCompletionAsync()
    {
        Buffer.Text = "SELECT * FROM free"; Primary.CaretOffset = Buffer.TextLength; Primary.Focus();
        await ShowCompletionAsync(Primary);
        if (completionWindow == null || !completionWindow.CompletionList.CompletionData.Any(c => c.Text == "freecolumns")) throw new InvalidOperationException("Completion popup missing freecolumns");
        completionWindow.CompletionList.SelectItem("freecolumns");
        completionWindow.CompletionList.RequestInsertion(EventArgs.Empty);
        if (Buffer.Text != "SELECT * FROM [dbo].[freecolumns]") throw new InvalidOperationException("Completion prefix replacement failed");
    }
    public void Dispose()
    {
        disposed = true; completionWindow?.Close();
        foreach (var manager in foldingManagers) FoldingManager.Uninstall(manager);
        foldingManagers.Clear();
    }
    internal async Task SmokeNarrowingCompletionAsync()
    {
        CompletionLoader = () => Task.FromResult<IReadOnlyList<SqlSymbol>>(Enumerable.Range(0, 500).Select(i => new SqlSymbol("dbo", "dm" + i.ToString("D4"), null, "table")).Append(new("dbo", "dmbp", null, "table")).ToList());
        Buffer.Text = "SELECT * FROM dm"; Primary.CaretOffset = Buffer.TextLength; Primary.Focus();
        await ShowCompletionAsync(Primary);
        Buffer.Text = "SELECT * FROM dmbp"; Primary.CaretOffset = Buffer.TextLength;
        await ShowCompletionAsync(Primary);
        if (completionWindow == null || !completionWindow.CompletionList.CompletionData.Any(c => c.Text == "dmbp")) throw new InvalidOperationException("dmbp omitted after narrowing more than 200 matches");
        completionWindow.CompletionList.SelectItem("dmbp"); completionWindow.CompletionList.RequestInsertion(EventArgs.Empty);
        if (Buffer.Text != "SELECT * FROM [dbo].[dmbp]") throw new InvalidOperationException("dmbp completion insertion");
    }
    public void ReloadFromDisk(FileSnapshot snapshot)
    {
        Buffer.Replace(0, Buffer.TextLength, snapshot.Text);
        Snapshot = snapshot;
        Changed?.Invoke();
        UpdateFoldings();
    }
    public void RefreshHighlighting(string theme)
    {
        var definition = CreateHighlighting(Language, theme);
        if (!ReferenceEquals(Primary.SyntaxHighlighting, definition)) Primary.SyntaxHighlighting = definition;
        if (Secondary != null && !ReferenceEquals(Secondary.SyntaxHighlighting, definition)) Secondary.SyntaxHighlighting = definition;
    }
    private static readonly Dictionary<(string Language, string Theme), IHighlightingDefinition?> highlightingCache = [];
    private static IHighlightingDefinition? CreateHighlighting(string language, string theme)
    {
        language = language is ".xaml" or ".config" or ".html" or ".aspx" or ".f" or ".ent" ? ".xml" : language == ".ts" ? ".js" : language;
        var key = (language, theme);
        if (!highlightingCache.TryGetValue(key, out var definition)) highlightingCache[key] = definition = BuildHighlighting(language, theme);
        return definition;
    }
    private static IHighlightingDefinition? BuildHighlighting(string language, string theme)
    {
        if (language is ".sql" or ".xml" or ".xaml" or ".config" or ".html" or ".aspx" or ".f" or ".ent" or ".js" or ".ts")
            return LSCode.Shell.Services.EmbeddedHighlighting.Create(language, theme);
        var keyword = theme == "Light" ? "#005FB8" : theme == "High Contrast" ? "#FFFF00" : "#79B8FF";
        var literal = theme == "Light" ? "#9E4A13" : theme == "High Contrast" ? "#FFFFFF" : "#E9B58D";
        var comment = theme == "Light" ? "#477044" : theme == "High Contrast" ? "#BFFF00" : "#91B98D";
        var attribute = theme == "Light" ? "#7B308C" : theme == "High Contrast" ? "#00FFFF" : "#BCA8F5";
        var entity = theme == "Light" ? "#8A3FFC" : theme == "High Contrast" ? "#FF00FF" : "#D58CFF";
        var include = theme == "Light" ? "#007A6E" : theme == "High Contrast" ? "#00FFFF" : "#63D9BB";
        var cdata = theme == "Light" ? "#C41A16" : theme == "High Contrast" ? "#FF6600" : "#FF7B72";
        var jsKeyword = theme == "Light" ? "#D73A49" : theme == "High Contrast" ? "#FFFF00" : "#FF7B72";
        var jsFunc = theme == "Light" ? "#6F42C1" : theme == "High Contrast" ? "#00FFFF" : "#D2A8FF";
        var sqlKeyword = theme == "Light" ? "#0000FF" : theme == "High Contrast" ? "#00FFFF" : "#56C8FF";
        var xshd = """
        <SyntaxDefinition name="LS SQL" extensions=".sql" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="Comment" foreground="$COMMENT" fontStyle="italic"/>
          <Color name="String" foreground="$STRING"/>
          <Color name="Keyword" foreground="$KEYWORD" fontWeight="bold"/>
          <RuleSet ignoreCase="true">
            <Span color="Comment" begin="--"/>
            <Span color="Comment" multiline="true" begin="/\*" end="\*/"/>
            <Span color="String" multiline="true" begin="'" end="'"/>
            <Keywords color="Keyword"><Word>SELECT</Word><Word>FROM</Word><Word>WHERE</Word><Word>JOIN</Word><Word>LEFT</Word><Word>RIGHT</Word><Word>INNER</Word><Word>ON</Word><Word>AS</Word><Word>AND</Word><Word>OR</Word><Word>NOT</Word><Word>NULL</Word><Word>IS</Word><Word>INSERT</Word><Word>INTO</Word><Word>UPDATE</Word><Word>DELETE</Word><Word>CREATE</Word><Word>ALTER</Word><Word>PROCEDURE</Word><Word>EXEC</Word><Word>DECLARE</Word><Word>SET</Word><Word>BEGIN</Word><Word>END</Word><Word>GO</Word><Word>CASE</Word><Word>WHEN</Word><Word>THEN</Word><Word>ELSE</Word><Word>ORDER</Word><Word>GROUP</Word><Word>BY</Word><Word>HAVING</Word><Word>TOP</Word><Word>DISTINCT</Word><Word>UNION</Word><Word>ALL</Word><Word>VALUES</Word><Word>WITH</Word></Keywords>
          </RuleSet>
        </SyntaxDefinition>
        """;
        if (language is ".cs" or ".js" or ".ts" or ".json" or ".css") xshd = """
        <SyntaxDefinition name="LS Code" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="Comment" foreground="$COMMENT" fontStyle="italic"/>
          <Color name="String" foreground="$STRING"/>
          <Color name="Keyword" foreground="$KEYWORD" fontWeight="bold"/>
          <Color name="Function" foreground="$JSFUNC"/>
          <RuleSet>
            <Span color="Comment" begin="//"/>
            <Span color="Comment" multiline="true" begin="/\*" end="\*/"/>
            <Span color="String" begin="&quot;" end="&quot;"/>
            <Span color="String" begin="'" end="'"/>
            <Span color="String" begin="`" end="`"/>
            <Keywords color="Keyword"><Word>using</Word><Word>namespace</Word><Word>class</Word><Word>public</Word><Word>private</Word><Word>static</Word><Word>var</Word><Word>const</Word><Word>let</Word><Word>function</Word><Word>return</Word><Word>if</Word><Word>else</Word><Word>for</Word><Word>foreach</Word><Word>while</Word><Word>switch</Word><Word>case</Word><Word>break</Word><Word>new</Word><Word>null</Word><Word>true</Word><Word>false</Word><Word>async</Word><Word>await</Word><Word>import</Word><Word>export</Word><Word>try</Word><Word>catch</Word><Word>this</Word><Word>typeof</Word><Word>instanceof</Word></Keywords>
            <Rule color="Function">\$\w+</Rule>
            <Rule color="Function">\b\w+(?=\s*\()</Rule>
          </RuleSet>
        </SyntaxDefinition>
        """;
        else if (language != ".sql") return null;
        xshd = xshd.Replace("$COMMENT", comment).Replace("$STRING", literal).Replace("$KEYWORD", keyword)
            .Replace("$ATTRIBUTE", attribute).Replace("$ENTITY", entity).Replace("$INCLUDE", include)
            .Replace("$CDATA", cdata).Replace("$JSKEYWORD", jsKeyword).Replace("$JSFUNC", jsFunc)
            .Replace("$SQLKEYWORD", sqlKeyword);
        using var reader = XmlReader.Create(new System.IO.StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
    private sealed class KeywordCompletion(SqlSuggestion suggestion, Action close, bool preservePlaceholders) : ICompletionData
    {
        public ImageSource? Image => null;
        public string Text => suggestion.Text;
        public object Content => suggestion.Text;
        public object Description => suggestion.Description;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var expanded = suggestion.IsSnippet ? CompletionSnippets.Expand(suggestion.InsertText, preservePlaceholders) : (Text: suggestion.InsertText, Offset: suggestion.InsertText.Length, Length: 0);
            var start = completionSegment.Offset;
            textArea.Document.Replace(completionSegment, expanded.Text);
            textArea.Caret.Offset = start + expanded.Offset;
            if (expanded.Length > 0) textArea.Selection = Selection.Create(textArea, start + expanded.Offset, start + expanded.Offset + expanded.Length);
            close();
        }
    }
    public void ToggleSplit()
    {
        if (Secondary != null)
        {
            if (foldingManagers.Count > 1) { FoldingManager.Uninstall(foldingManagers[^1]); foldingManagers.RemoveAt(foldingManagers.Count - 1); }
            Layout.Children.Clear(); Layout.ColumnDefinitions.Clear(); Secondary = null;
            ActiveEditor = Primary; Layout.Children.Add(Primary); return;
        }
        Layout.ColumnDefinitions.Add(new ColumnDefinition());
        Layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        Layout.ColumnDefinitions.Add(new ColumnDefinition());
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
        splitter.SetResourceReference(Control.BackgroundProperty, "Border"); Grid.SetColumn(splitter, 1); Layout.Children.Add(splitter);
        Secondary = CreateEditor(); Grid.SetColumn(Secondary, 2); Layout.Children.Add(Secondary);
    }
}





