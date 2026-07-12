using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Silmoon.Intelligence.Client.Controls
{
    public class MarkdownBlock : ContentControl
    {
        public static readonly StyledProperty<string> MarkdownProperty =
            AvaloniaProperty.Register<MarkdownBlock, string>(nameof(Markdown), string.Empty);

        public string Markdown
        {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MarkdownProperty)
                Content = BuildContent(Markdown);
        }

        static Control BuildContent(string markdown)
        {
            var panel = new StackPanel { Spacing = 8 };
            var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var paragraph = new List<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushParagraph(panel, paragraph);
                    var codeLines = new List<string>();
                    i++;
                    while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                    {
                        codeLines.Add(lines[i]);
                        i++;
                    }

                    panel.Children.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
                    continue;
                }

                if (IsTableStart(lines, i))
                {
                    FlushParagraph(panel, paragraph);
                    var tableLines = new List<string> { lines[i] };
                    i += 2;
                    while (i < lines.Length && IsPipeRow(lines[i]))
                    {
                        tableLines.Add(lines[i]);
                        i++;
                    }
                    i--;

                    panel.Children.Add(CreateTable(tableLines));
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    FlushParagraph(panel, paragraph);
                    continue;
                }

                if (trimmed is "---" or "***" or "___")
                {
                    FlushParagraph(panel, paragraph);
                    panel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = Brush.Parse("#DDE4EE"),
                        Margin = new Thickness(0, 4)
                    });
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    FlushParagraph(panel, paragraph);
                    var level = trimmed.TakeWhile(c => c == '#').Count();
                    var text = trimmed[level..].Trim();
                    panel.Children.Add(CreateText(text, level <= 2 ? 17 : 15, FontWeight.SemiBold, "#1F2937"));
                    continue;
                }

                if (trimmed.StartsWith("> ", StringComparison.Ordinal))
                {
                    FlushParagraph(panel, paragraph);
                    panel.Children.Add(new Border
                    {
                        BorderBrush = Brush.Parse("#C9D3E4"),
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        Padding = new Thickness(10, 2, 0, 2),
                        Child = CreateText(trimmed[2..], 13, FontWeight.Normal, "#596276")
                    });
                    continue;
                }

                if (IsListItem(trimmed, out var listText))
                {
                    FlushParagraph(panel, paragraph);
                    panel.Children.Add(CreateListItem(listText));
                    continue;
                }

                paragraph.Add(line);
            }

            FlushParagraph(panel, paragraph);
            return panel;
        }

        static void FlushParagraph(StackPanel panel, List<string> paragraph)
        {
            if (paragraph.Count == 0) return;
            panel.Children.Add(CreateText(string.Join(Environment.NewLine, paragraph).Trim(), 13, FontWeight.Normal, "#273142"));
            paragraph.Clear();
        }

        static TextBlock CreateText(string text, double size, FontWeight weight, string color) =>
            new()
            {
                Text = CleanInline(text),
                TextWrapping = TextWrapping.Wrap,
                FontSize = size,
                FontWeight = weight,
                Foreground = Brush.Parse(color),
                LineHeight = Math.Max(20, size + 7)
            };

        static Border CreateCodeBlock(string code) =>
            new()
            {
                Background = Brush.Parse("#F4F6FA"),
                BorderBrush = Brush.Parse("#DDE4EE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8),
                Child = new TextBlock
                {
                    Text = code,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 12,
                    Foreground = Brush.Parse("#1F2937"),
                    LineHeight = 18
                }
            };

        static Grid CreateTable(List<string> rows)
        {
            var values = rows.Select(ParsePipeRow).Where(x => x.Count > 0).ToList();
            var columnCount = values.Max(x => x.Count);
            var grid = new Grid
            {
                RowSpacing = 0,
                ColumnSpacing = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            for (var col = 0; col < columnCount; col++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var row = 0; row < values.Count; row++)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var row = 0; row < values.Count; row++)
            {
                for (var col = 0; col < columnCount; col++)
                {
                    var cellText = col < values[row].Count ? values[row][col] : string.Empty;
                    var cell = new Border
                    {
                        Background = Brush.Parse(row == 0 ? "#F4F6FA" : "#FFFFFF"),
                        BorderBrush = Brush.Parse("#DDE4EE"),
                        BorderThickness = new Thickness(1, 1, col == columnCount - 1 ? 1 : 0, row == values.Count - 1 ? 1 : 0),
                        Padding = new Thickness(9, 7),
                        Child = CreateText(cellText, 12, row == 0 ? FontWeight.SemiBold : FontWeight.Normal, "#273142")
                    };
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    grid.Children.Add(cell);
                }
            }

            return grid;
        }

        static Border CreateListItem(string text)
        {
            var marker = new TextBlock
            {
                Text = "•",
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = Brush.Parse("#596276"),
                FontSize = 13
            };
            var body = CreateText(text, 13, FontWeight.Normal, "#273142");
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            Grid.SetColumn(body, 1);
            grid.Children.Add(marker);
            grid.Children.Add(body);

            return new Border { Child = grid };
        }

        static bool IsListItem(string line, out string text)
        {
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                text = line[2..].Trim();
                return true;
            }

            var dot = line.IndexOf('.');
            if (dot > 0 && dot < 4 && line[..dot].All(char.IsDigit))
            {
                text = line[(dot + 1)..].Trim();
                return text.Length > 0;
            }

            text = string.Empty;
            return false;
        }

        static bool IsTableStart(string[] lines, int index) =>
            index + 1 < lines.Length && IsPipeRow(lines[index]) && IsTableSeparator(lines[index + 1]);

        static bool IsPipeRow(string line) => line.Trim().Contains('|');

        static bool IsTableSeparator(string line)
        {
            var cells = ParsePipeRow(line);
            return cells.Count > 0 && cells.All(cell => cell.Length > 0 && cell.All(c => c is '-' or ':' or ' '));
        }

        static List<string> ParsePipeRow(string line) =>
            line.Trim().Trim('|').Split('|').Select(x => CleanInline(x.Trim())).ToList();

        static string CleanInline(string text) =>
            text.Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Replace("`", string.Empty);
    }
}
