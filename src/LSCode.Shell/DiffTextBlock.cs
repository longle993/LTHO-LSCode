using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LSCode.Core;

namespace LSCode.Shell;

public sealed class DiffTextBlock : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(DiffTextBlock), new PropertyMetadata("", Update));
    public static readonly DependencyProperty OtherProperty = DependencyProperty.Register(nameof(Other), typeof(string), typeof(DiffTextBlock), new PropertyMetadata("", Update));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Other { get => (string)GetValue(OtherProperty); set => SetValue(OtherProperty, value); }
    private static void Update(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var block = (DiffTextBlock)sender;
        var value = block.Value ?? ""; var other = block.Other ?? "";
        var span = DiffPresentation.ChangedSpan(value, other);
        block.Inlines.Clear();
        block.Inlines.Add(new Run(value[..span.Start]));
        if (span.Length > 0) block.Inlines.Add(new Run(value.Substring(span.Start, span.Length)) { Background = new SolidColorBrush(Color.FromArgb(80, 160, 160, 100)), FontWeight = FontWeights.SemiBold });
        block.Inlines.Add(new Run(value[(span.Start + span.Length)..]));
    }
}
