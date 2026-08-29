using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;

namespace RenoDXCommander;

/// <summary>
/// Runtime UI localisation (zh-Hans). English literals remain the source of
/// truth in code and XAML; <see cref="Tr"/> looks them up in a static table
/// (<see cref="LocZhHans"/>) and falls back to the input text when no
/// translation exists or localisation is disabled.
/// Localisation turns on automatically when the Windows UI language is
/// Chinese. Override by writing <c>zh</c> or <c>en</c> to
/// %LocalAppData%\RHI\language.txt.
/// </summary>
public static class Loc
{
    private static bool _enabled;
    private static readonly Lazy<Dictionary<string, string>> _table = new(BuildTable);

    /// <summary>Whether UI translation is active for this session.</summary>
    public static bool Enabled => _enabled;

    /// <summary>
    /// Resolves the session language. Call once at startup, before any UI is
    /// built. Never throws — on any failure localisation stays off and the
    /// app renders its original English strings.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            string overrideFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RHI", "language.txt");
            if (File.Exists(overrideFile))
            {
                string value = File.ReadAllText(overrideFile).Trim().ToLowerInvariant();
                if (value.Length > 0 && (value[0] is 'z' or 'e'))
                {
                    _enabled = value.StartsWith("zh");
                    return;
                }
            }
            _enabled = IsChineseUiLanguage();
        }
        catch
        {
            _enabled = false;
        }
    }

    private static bool IsChineseUiLanguage()
    {
        try
        {
            return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Translates an English UI literal. Returns the input unchanged when
    /// localisation is off or no entry exists, so call sites are always safe.
    /// </summary>
    public static string Tr(string text)
    {
        if (!_enabled || string.IsNullOrEmpty(text))
            return text;
        return _table.Value.TryGetValue(text, out string? translated) ? translated : text;
    }

    private static Dictionary<string, string> BuildTable() => LocZhHans.Entries();

    // ── Static XAML translation ──────────────────────────────────────────────
    // The XAML-declared shell (MainWindow / SetupWindow) carries its strings
    // as attributes, which Tr() cannot wrap at the call site. Apply() walks
    // the declared tree once after InitializeComponent and translates the
    // display properties. Combo/list item content is deliberately left alone:
    // selection values are mapped back to INI keys by string comparison.

    /// <summary>Translates a window's XAML-declared shell. Call once, right after InitializeComponent.</summary>
    public static void Apply(Window window)
    {
        if (!_enabled)
            return;
        try
        {
            window.Title = Tr(window.Title);
            if (window.Content is DependencyObject content)
                Translate(content);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[Loc.Apply] failed — {ex.Message}");
        }
    }

    private static void Translate(DependencyObject element)
    {
        // Values that feed selection-mapping logic must stay English.
        if (element is ComboBoxItem or ListBoxItem)
            return;

        switch (element)
        {
            case TextBlock textBlock:
                textBlock.Text = Tr(textBlock.Text);
                TranslateInlines(textBlock.Inlines);
                break;
            case RichTextBlock richBlock:
                foreach (Block block in richBlock.Blocks)
                    if (block is Paragraph paragraph)
                        TranslateInlines(paragraph.Inlines);
                break;
            case ButtonBase button when button.Content is string content:
                button.Content = Tr(content);
                break;
            case ToggleSwitch toggle:
                if (toggle.Header is string header) toggle.Header = Tr(header);
                if (toggle.OnContent is string on) toggle.OnContent = Tr(on);
                if (toggle.OffContent is string off) toggle.OffContent = Tr(off);
                break;
            case TextBox textBox:
                textBox.PlaceholderText = Tr(textBox.PlaceholderText);
                if (textBox.Header is string tbHeader) textBox.Header = Tr(tbHeader);
                break;
            case AutoSuggestBox suggest:
                suggest.PlaceholderText = Tr(suggest.PlaceholderText);
                if (suggest.Header is string sHeader) suggest.Header = Tr(sHeader);
                break;
            case ScrollViewer scrollViewer:
                if (scrollViewer.Content is DependencyObject scrollChild)
                    Translate(scrollChild);
                break;
            case ContentControl contentControl:
                switch (contentControl.Content)
                {
                    case string text:
                        contentControl.Content = Tr(text);
                        break;
                    case DependencyObject child:
                        Translate(child);
                        break;
                }
                break;
        }

        if (ToolTipService.GetToolTip(element) is string toolTip)
            ToolTipService.SetToolTip(element, Tr(toolTip));

        // Structural recursion — panels, borders, presenters, user controls.
        if (element is Panel panel)
        {
            foreach (UIElement child in panel.Children)
                if (child is DependencyObject childObject)
                    Translate(childObject);
        }
        else if (element is Border border && border.Child is DependencyObject borderChild)
        {
            Translate(borderChild);
        }
        else if (element is Viewbox viewbox && viewbox.Child is DependencyObject viewboxChild)
        {
            Translate(viewboxChild);
        }
        else if (element is ContentPresenter presenter)
        {
            switch (presenter.Content)
            {
                case string presenterText:
                    presenter.Content = Tr(presenterText);
                    break;
                case DependencyObject presenterChild:
                    Translate(presenterChild);
                    break;
            }
        }
    }

    private static void TranslateInlines(InlineCollection inlines)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    run.Text = Tr(run.Text);
                    break;
                // Fully qualified: a bare "Span" is ambiguous with System.Span<T>
                // brought in by implicit usings.
                case Microsoft.UI.Xaml.Documents.Span span:
                    TranslateInlines(span.Inlines);
                    break;
            }
        }
    }
}
