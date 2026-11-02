using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace LSCode.Shell.Services;
internal static class EmbeddedHighlightingChecks
{
    internal const string Fixture = """
    <!DOCTYPE dir [
    <!ENTITY CommandQuery "
    SELECT field FROM table_name WHERE value = 'select from'; -- SQL comment
    ">
    <!ENTITY ScriptInit "
    function on$Voucher$GridRender(sender) { return 'function inside string'; }
    ">
    ]>
    <dir>
    <command event="Loading"><text><![CDATA[
    select name from customers where code = 'it''s SELECT';
    /* from inside SQL comment */
    ]]></text></command>
    <script><![CDATA[
    // function on$Voucher$GridRender is a comment
    function on$Voucher$GridRender(sender, eventArgs) {
      var value = 'it\'s function'; on$Voucher$GridRender(sender);
      /* comment */ return value;
    }
    ]]>&ScriptCurrency;<![CDATA[
    function set$Voucher$GridColumns() { return `string function`; }
    ]]></script>
    <field name="AfterScript" />
    </dir>
    """;
    public static void Run()
    {
        foreach (var theme in new[] { "Dark", "Light", "High Contrast" })
        {
            var doc = new TextDocument(Fixture);
            using var highlighter = new DocumentHighlighter(doc, EmbeddedHighlighting.Create(".xml", theme));
            void Check(int line, string token, string color)
            {
                var text = doc.GetText(doc.GetLineByNumber(line));
                var start = doc.GetLineByNumber(line).Offset + text.IndexOf(token, StringComparison.Ordinal);
                var sections = highlighter.HighlightLine(line).Sections;
                if (!sections.Any(s => s.Offset <= start && s.Offset + s.Length >= start + token.Length && s.Color.Name == color))
                    throw new InvalidOperationException($"{theme}: line {line}, {token} expected {color}; got " + string.Join(",", sections.Select(s => s.Color.Name + ":" + doc.GetText(s.Offset, s.Length))) + " previous: " + string.Join(" / ", Enumerable.Range(Math.Max(1, line - 3), Math.Min(3, line - 1)).Select(n => n + ":" + string.Join(",", highlighter.HighlightLine(n).Sections.Select(s => s.Color.Name + ":" + doc.GetText(s.Offset, s.Length))))));
            }
            Check(3, "SELECT", "Keyword"); Check(3, "FROM", "Keyword"); Check(3, "'select from'", "String"); Check(3, "-- SQL comment", "Comment");
            Check(6, "function", "Keyword"); Check(6, "on$Voucher$GridRender", "Function");
            Check(11, "select", "Keyword"); Check(11, "where", "Keyword"); Check(12, "/* from inside SQL comment */", "Comment");
            Check(10, "<![CDATA[", "CData"); Check(14, "<![CDATA[", "CData");
            Check(15, "function on$Voucher$GridRender", "Comment"); Check(16, "function", "Keyword"); Check(16, "on$Voucher$GridRender", "Function");
            Check(17, "'it\\'s function'".Replace("\\\\", "\\"), "String"); Check(17, "on$Voucher$GridRender", "Function"); Check(18, "return", "Keyword");
            Check(21, "set$Voucher$GridColumns", "Function"); Check(23, "name", "Attribute");
            var sample = new TextDocument("<!ENTITY Delta14 \"0\">\n<field name=\"x\"><![CDATA[\ndeclare @x int; select @x;\n]]></field>\n<script><![CDATA[\nf.add_onResponseComplete(on$Filter$ResponseComplete);\n]]>&ReportSign.Filter.Active;<![CDATA[\n}]]></script>\n<!ENTITY External SYSTEM \"path.ent\">");
            using var sampleHighlighter = new DocumentHighlighter(sample, EmbeddedHighlighting.Create(".xml", theme));
            void Token(int line, string token, string color)
            {
                var l = sample.GetLineByNumber(line);
                var start = l.Offset + sample.GetText(l).IndexOf(token, StringComparison.Ordinal);
                if (!sampleHighlighter.HighlightLine(line).Sections.Any(s => s.Offset <= start && s.Offset + s.Length >= start + token.Length && s.Color.Name == color))
                    throw new InvalidOperationException($"{theme}: {token} must be {color}");
            }
            Token(1, "<!ENTITY", "Declaration"); Token(1, "Delta14", "Entity"); Token(1, ">", "Declaration");
            Token(2, "<field", "Markup"); Token(2, "<![CDATA[", "CData");
            Token(3, "declare", "Keyword"); Token(3, "int", "Keyword"); Token(3, "select", "Keyword");
            Token(6, "f.add_onResponseComplete", "Function"); Token(6, "on$Filter$ResponseComplete", "Function");
            Token(7, "]]>", "CData"); Token(7, "&ReportSign.Filter.Active;", "Include");
            Token(9, "<!ENTITY", "Declaration"); Token(9, "External", "Entity"); Token(9, "SYSTEM", "Keyword"); Token(9, ">", "Declaration");
        }
        var js = new TextDocument("function on$Voucher$GridRender() { return 'x'; } // comment");
        using var standalone = new DocumentHighlighter(js, EmbeddedHighlighting.Create(".js", "Light"));
        if (!standalone.HighlightLine(1).Sections.Any(s => s.Color.Name == "Function" && js.GetText(s.Offset, s.Length) == "on$Voucher$GridRender")) throw new InvalidOperationException("Standalone JS dollar identifier split");
    }
}

