using System.IO;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace LSCode.Shell.Services;

public static class EmbeddedHighlighting
{
    public static IHighlightingDefinition Create(string language, string theme)
    {
        var source = """
        <SyntaxDefinition name="LS embedded languages" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="CData" foreground="$GREEN"/>
          <Color name="Include" foreground="$RED"/>
          <Color name="Declaration" foreground="$RED"/>
          <Color name="Keyword" foreground="$KEY" fontWeight="bold"/>
          <Color name="String" foreground="$STRING"/>
          <Color name="Comment" foreground="$COMMENT" fontStyle="italic"/>
          <Color name="Function" foreground="$FUNCTION"/>
          <Color name="Markup" foreground="$MARKUP"/>
          <Color name="Entity" foreground="$ENTITY"/>
          <Color name="Attribute" foreground="$ATTRIBUTE"/>
          <RuleSet name="SQL" ignoreCase="true">
            <Span color="Comment" begin="--"/>
            <Span color="Comment" multiline="true" begin="/\*" end="\*/"/>
            <Span color="String" multiline="true" begin="'" end="'"><RuleSet><Rule color="String">''</Rule></RuleSet></Span>
            <Rule color="String">\[[^\]\r\n]*\]</Rule>
            <Rule color="Keyword">(?i:\b(select|from|where|join|inner|left|right|outer|on|as|and|or|not|is|null|insert|into|values|update|delete|set|exec|execute|declare|begin|end|if|else|exists|case|when|then|order|group|by|having|union|all|distinct|top|with|return|while|create|alter|drop|table|procedure|cast|convert|count|sum|isnull|coalesce|like|in|between|asc|desc|try|catch|transaction|commit|rollback|int|bigint|smallint|tinyint|bit|decimal|numeric|float|real|money|varchar|nvarchar|char|nchar|text|ntext|date|datetime|datetime2|time|uniqueidentifier|varbinary|binary|xml|output|nolock|truncate|temp)\b)</Rule>
          </RuleSet>
          <RuleSet name="JS">
            <Span color="Comment" begin="//"/>
            <Span color="Comment" multiline="true" begin="/\*" end="\*/"/>
            <Span color="String" begin="&quot;" end="&quot;"><RuleSet><Span begin="\\" end="."/></RuleSet></Span>
            <Span color="String" begin="'" end="'"><RuleSet><Span begin="\\" end="."/></RuleSet></Span>
            <Span color="String" multiline="true" begin="`" end="`"><RuleSet><Span begin="\\" end="."/></RuleSet></Span>
            <Rule color="Keyword">(?&lt;![\w$])(?:function|var|let|const|if|else|for|while|do|switch|case|break|continue|return|try|catch|finally|throw|new|this|true|false|null|undefined|typeof|instanceof|class|extends|import|export|async|await|yield|in|of|delete|void)(?![\w$])</Rule>
            <Rule color="Function">(?&lt;![\w$])[\p{L}_$][\w$]*(?:\.[\p{L}_$][\w$]*)*</Rule>
          </RuleSet>
          <RuleSet name="Boundaries">
            <Rule color="CData">&lt;!\[CDATA\[|\]\]&gt;</Rule>
            <Rule color="Include">[&amp;%][\w.$:-]+;</Rule>
            <Rule color="Markup">&lt;/?(?:text|query|sql)\b[^&gt;]*&gt;</Rule>
          </RuleSet>
          <RuleSet name="EmbeddedSQL"><Import ruleSet="Boundaries"/><Import ruleSet="SQL"/></RuleSet>
          <RuleSet name="EmbeddedJS"><Import ruleSet="Boundaries"/><Import ruleSet="JS"/></RuleSet>
          <RuleSet name="Mixed"><Import ruleSet="Boundaries"/><Import ruleSet="SQL"/><Import ruleSet="JS"/></RuleSet>
          <RuleSet name="XML">
            <Span color="Comment" multiline="true" begin="&lt;!--" end="--&gt;"/>
            <Span color="Entity" multiline="true" begin="&lt;!DOCTYPE\b" end="\[|&gt;"/>
            <Span color="Entity" multiline="true" begin="&lt;!ENTITY\s+%?\s*[\w.$:-]+\s+(?:SYSTEM|PUBLIC)\b" end="&gt;"/>
            <Span multiline="true" ruleSet="EmbeddedJS"><Begin color="Markup">(?i:&lt;script\b[^&gt;]*&gt;)</Begin><End color="Markup">(?i:&lt;/script\s*&gt;)</End></Span>
            <Span multiline="true" ruleSet="EmbeddedSQL"><Begin color="Markup">(?i:&lt;command\b[^&gt;]*&gt;)</Begin><End color="Markup">(?i:&lt;/command\s*&gt;)</End></Span>
            <Span multiline="true" ruleSet="EmbeddedJS"><Begin color="Entity">(?i:&lt;!ENTITY\s+%?\s*(?:Script|Javascript)[\w.$:-]*\s+&quot;)</Begin><End color="Entity">&quot;\s*&gt;</End></Span>
            <Span multiline="true" ruleSet="EmbeddedJS"><Begin color="Entity">(?i:&lt;!ENTITY\s+%?\s*(?:Script|Javascript)[\w.$:-]*\s+')</Begin><End color="Entity">'\s*&gt;</End></Span>
            <Rule color="Entity">&lt;!ENTITY\s+%?\s*[\w.$:-]+\s+(?:SYSTEM|PUBLIC)\s+(?:&quot;[^&quot;]*&quot;|'[^']*')\s*&gt;</Rule>
            <Span multiline="true" ruleSet="EmbeddedSQL"><Begin color="Entity">&lt;!ENTITY\s+%?\s*[\w.$:-]+\s+&quot;</Begin><End color="Entity">&quot;\s*&gt;</End></Span>
            <Span multiline="true" ruleSet="EmbeddedSQL"><Begin color="Entity">&lt;!ENTITY\s+%?\s*[\w.$:-]+\s+'</Begin><End color="Entity">'\s*&gt;</End></Span>
            <Rule color="Entity">&lt;!DOCTYPE\s+[^\[&gt;]+|\]&gt;</Rule>
            <Span multiline="true" ruleSet="Mixed"><Begin color="CData">&lt;!\[CDATA\[</Begin><End color="CData">\]\]&gt;</End></Span>
            <Rule color="Include">[&amp;%][\w.$:-]+;</Rule>
            <Span multiline="true"><Begin color="Markup">&lt;/?[\w:.-]+</Begin><End color="Markup">&gt;</End><RuleSet>
              <Span color="String" begin="&quot;" end="&quot;"/><Span color="String" begin="'" end="'"/>
              <Rule color="Attribute">[\w:.-]+(?=\s*=)</Rule>
            </RuleSet></Span>
          </RuleSet>
          <RuleSet><Import ruleSet="$ROOT"/></RuleSet>
        </SyntaxDefinition>
        """;
        bool light = theme == "Light", contrast = theme == "High Contrast";
        source = source.Replace("$ROOT", language == ".sql" ? "SQL" : language is ".js" or ".ts" ? "JS" : "XML")
            .Replace("$GREEN", light ? "#18683C" : contrast ? "#BFFF00" : "#63D990")
            .Replace("$RED", light ? "#9A246D" : contrast ? "#FF80FF" : "#F58BCB")
            .Replace("$KEY", light ? "#005FB8" : contrast ? "#FFFF00" : "#79B8FF")
            .Replace("$STRING", light ? "#8B3F0B" : contrast ? "#FFFFFF" : "#F2B77D")
            .Replace("$COMMENT", light ? "#466347" : contrast ? "#BFFF00" : "#8EC58A")
            .Replace("$FUNCTION", light ? "#192738" : "#E0E6EE")
            .Replace("$MARKUP", light ? "#0758AD" : contrast ? "#FFFF00" : "#65B5FF")
            .Replace("$ATTRIBUTE", light ? "#006975" : contrast ? "#00FFFF" : "#57D4DF")
            .Replace("$ENTITY", light ? "#7524A8" : contrast ? "#FF80FF" : "#D29CFF");
        // Entity delimiter quotes belong to XML, not to the embedded lexer. An XML
        // entity cannot contain its own unescaped delimiter as a code string opener.
        var xml = XDocument.Parse(source); XNamespace ns = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";
        foreach (var (name, quote) in new[] { ("JS", "\""), ("JS", "'"), ("SQL", "'") })
        {
            var copy = new XElement(xml.Root!.Elements(ns + "RuleSet").Single(r => (string?)r.Attribute("name") == name));
            var copyName = name + (quote == "\"" ? "EntityDouble" : "EntitySingle"); copy.SetAttributeValue("name", copyName);
            copy.Elements(ns + "Span").Where(s => (string?)s.Attribute("begin") == quote).Remove();
            xml.Root.Add(copy);
            foreach (var span in xml.Descendants(ns + "Span").Where(s => s.Element(ns + "Begin")?.Value.Contains("!ENTITY") == true && (string?)s.Attribute("ruleSet") == "Embedded" + name && s.Element(ns + "End")!.Value.StartsWith(quote)))
                span.SetAttributeValue("ruleSet", copyName);
        }
        var xmlRules = xml.Root!.Elements(ns + "RuleSet").Single(r => (string?)r.Attribute("name") == "XML");
        var entitySpans = xmlRules.Elements(ns + "Span").Where(s => (s.Element(ns + "Begin")?.Value ?? (string?)s.Attribute("begin") ?? "").Contains("!ENTITY")).ToList();
        foreach (var span in entitySpans)
        {
            var begin = span.Element(ns + "Begin");
            if (begin == null) { span.Remove(); continue; }
            var quote = span.Element(ns + "End")!.Value.StartsWith("\"") ? "\"" : "'";
            var bodyRules = (string?)span.Attribute("ruleSet");
            var isJs = bodyRules!.StartsWith("JS");
            var prefix = isJs
                ? @"(?i:<!ENTITY\s+(?=%?\s*(?:Script|Javascript)[\w.$:-]*\s+" + quote + "))"
                : @"<!ENTITY\s+(?=%?\s*[\w.$:-]+\s+" + quote + ")";
            span.ReplaceWith(new XElement(ns + "Span", new XAttribute("multiline", "true"),
                new XElement(ns + "Begin", new XAttribute("color", "Declaration"), prefix),
                new XElement(ns + "End", new XAttribute("color", "Declaration"), ">"),
                new XElement(ns + "RuleSet",
                    new XElement(ns + "Span", new XAttribute("multiline", "true"), new XAttribute("ruleSet", bodyRules!), new XAttribute("begin", quote), new XAttribute("end", quote)),
                    new XElement(ns + "Rule", new XAttribute("color", "Entity"), @"%?[\w.$:-]+"))));
        }
        xmlRules.AddFirst(new XElement(ns + "Span", new XAttribute("multiline", "true"),
            new XElement(ns + "Begin", new XAttribute("color", "Declaration"), @"<!ENTITY\s+(?=%?\s*[\w.$:-]+\s+(?:SYSTEM|PUBLIC)\b)"),
            new XElement(ns + "End", new XAttribute("color", "Declaration"), ">"),
            new XElement(ns + "RuleSet",
                new XElement(ns + "Span", new XAttribute("color", "String"), new XAttribute("begin", "\""), new XAttribute("end", "\"")),
                new XElement(ns + "Span", new XAttribute("color", "String"), new XAttribute("begin", "'"), new XAttribute("end", "'")),
                new XElement(ns + "Rule", new XAttribute("color", "Keyword"), @"\b(?:SYSTEM|PUBLIC)\b"),
                new XElement(ns + "Rule", new XAttribute("color", "Entity"), @"%?[\w.$:-]+"))));
        using var reader = xml.CreateReader();
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}






