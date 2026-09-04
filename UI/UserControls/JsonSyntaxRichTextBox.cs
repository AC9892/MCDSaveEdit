using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MCDSaveEdit.UI
{
    public sealed class JsonSyntaxRichTextBox : RichTextBox
    {
        private const int MaximumHighlightedCharacters = 50000;
        private const int MaximumPreviewCharacters = 200000;
        private static readonly Brush PropertyBrush = new SolidColorBrush(Color.FromRgb(111, 196, 255));
        private static readonly Brush StringBrush = new SolidColorBrush(Color.FromRgb(226, 176, 107));
        private static readonly Brush NumberBrush = new SolidColorBrush(Color.FromRgb(152, 195, 121));
        private static readonly Brush LiteralBrush = new SolidColorBrush(Color.FromRgb(198, 120, 221));
        private static readonly Brush PunctuationBrush = new SolidColorBrush(Color.FromRgb(128, 139, 153));
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(231, 121, 121));

        public JsonSyntaxRichTextBox()
        {
            IsReadOnly = true;
            IsUndoEnabled = false;
            FontFamily = new FontFamily("Consolas");
            FontSize = 12;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            Document.PageWidth = 4000;
        }

        public void SetJson(string json)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 17 };
            if (string.IsNullOrWhiteSpace(json) || (!json.TrimStart().StartsWith("{") && !json.TrimStart().StartsWith("[")))
            {
                paragraph.Inlines.Add(new Run(json ?? string.Empty) { Foreground = ErrorBrush });
                Document.Blocks.Clear();
                Document.Blocks.Add(paragraph);
                return;
            }

            // A FlowDocument with tens of thousands of individually colored Runs can
            // block the WPF UI thread for long enough to trigger AppHangB1. Large
            // monitor sections stay readable as one lightweight run; full content is
            // still available through Copy and the automatic snapshot export.
            if (json.Length > MaximumHighlightedCharacters)
            {
                bool truncated = json.Length > MaximumPreviewCharacters;
                string preview = truncated ? json.Substring(0, MaximumPreviewCharacters) : json;
                if (truncated)
                    preview += $"\n\n[Preview truncated: showing {MaximumPreviewCharacters:N0} of {json.Length:N0} characters. Copy or open the exported snapshot for the complete JSON.]";
                paragraph.Inlines.Add(new Run(preview) { Foreground = Foreground });
                Document.Blocks.Clear();
                Document.Blocks.Add(paragraph);
                ToolTip = truncated
                    ? "Large JSON preview truncated for responsiveness; Copy Current Tab and exported snapshots remain complete."
                    : "Syntax highlighting disabled for this large section to keep the live monitor responsive.";
                return;
            }
            ToolTip = null;

            int index = 0;
            while (index < json.Length)
            {
                int start = index;
                char current = json[index];
                Brush brush;
                if (char.IsWhiteSpace(current))
                {
                    while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
                    brush = Foreground;
                }
                else if (current == '"')
                {
                    index++;
                    bool escaped = false;
                    while (index < json.Length)
                    {
                        char value = json[index++];
                        if (value == '"' && !escaped) break;
                        escaped = value == '\\' && !escaped;
                        if (value != '\\') escaped = false;
                    }
                    int lookAhead = index;
                    while (lookAhead < json.Length && char.IsWhiteSpace(json[lookAhead])) lookAhead++;
                    brush = lookAhead < json.Length && json[lookAhead] == ':' ? PropertyBrush : StringBrush;
                }
                else if (char.IsDigit(current) || current == '-')
                {
                    index++;
                    while (index < json.Length && "0123456789.eE+-".IndexOf(json[index]) >= 0) index++;
                    brush = NumberBrush;
                }
                else if (char.IsLetter(current))
                {
                    index++;
                    while (index < json.Length && char.IsLetter(json[index])) index++;
                    brush = LiteralBrush;
                }
                else
                {
                    index++;
                    brush = PunctuationBrush;
                }
                paragraph.Inlines.Add(new Run(json.Substring(start, index - start)) { Foreground = brush });
            }

            Document.Blocks.Clear();
            Document.Blocks.Add(paragraph);
        }
    }
}
