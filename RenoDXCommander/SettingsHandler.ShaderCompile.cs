// SettingsHandler.ShaderCompile.cs -- Settings › Driver Settings › Driver defaults: "Compile shaders now" row under
// Shader pre-compile (built in C#, XAML constraint). Runs NVIDIA's shader compiler straight away, like the NVIDIA
// App's Shader Cache › Compile now, at the Shader pre-compile level. See NvShaderCompilerService.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private bool _shaderCompileRowAdded;
    private Button? _shaderCompileBtn;
    private TextBlock? _shaderCompileStatus;
    private ProgressBar? _shaderCompileBar;
    private bool _shaderCompileOurs; // RHI started the run being shown

    private static string ShaderCompileIdleText => Loc.GetString("Settings.ShaderCompile.CompileNow");

    private void EnsureShaderCompileRow()
    {
        if (_shaderCompileRowAdded) return;
        _shaderCompileRowAdded = true;
        try
        {
            var path = new List<UIElement>();
            bool found = FindPath(_window.SettingsCardsPanel, _window.ShaderPrecompileCombo, path);
            int ci = found ? path.FindLastIndex(e => e is StackPanel) : -1; // the column; the next element is the row
            if (ci < 0 || ci + 1 >= path.Count || path[ci] is not StackPanel column || path[ci + 1] is not FrameworkElement row)
            {
                CrashReporter.Log("[SettingsHandler.EnsureShaderCompileRow] Shader pre-compile row not found, not added");
                return;
            }

            _shaderCompileBtn = new Button { Content = ShaderCompileIdleText, Style = FindStyle("SettingsButtonStyle") };
            if (Application.Current.Resources.TryGetValue(ResourceKeys.SettingsLabelBrush, out var v) && v is Brush brush)
            {
                _shaderCompileBtn.Foreground = brush;
                _shaderCompileBtn.Resources["ButtonForegroundPointerOver"] = brush;
                _shaderCompileBtn.Resources["ButtonForegroundPressed"] = brush;
            }
            ToolTipService.SetToolTip(_shaderCompileBtn, Loc.GetString("Settings.ShaderCompile.Button.Tooltip"));
            _shaderCompileBtn.Click += ShaderCompileBtn_Click;

            _shaderCompileStatus = new TextBlock
            {
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };

            _shaderCompileBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            int at = column.Children.IndexOf(row) + 1;
            column.Children.Insert(at, SettingRow(Loc.GetString("Settings.ShaderCompile.RowLabel"), _shaderCompileBtn));
            column.Children.Insert(at + 1, _shaderCompileStatus);
            column.Children.Insert(at + 2, _shaderCompileBar);

            if (NvShaderCompilerService.FindCompiler() == null)
            {
                _shaderCompileBtn.IsEnabled = false;
                SetShaderCompileStatus(Loc.GetString("Settings.ShaderCompile.Status.NeedsCompiler"));
            }
            else if (NvShaderCompilerService.IsRunning())
            {
                ShowShaderCompileRunning(ours: false);
                _ = WatchOtherShaderCompileAsync();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureShaderCompileRow] Failed — {ex.Message}");
        }
    }

    private async void ShaderCompileBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_shaderCompileBtn == null) return;

            // Running (ours, the NVIDIA App's or the idle task's): the button stops it
            if (NvShaderCompilerService.IsRunning())
            {
                await Task.Run(NvShaderCompilerService.Stop);
                ShowShaderCompileIdle();
                SetShaderCompileStatus(Loc.GetString("Settings.ShaderCompile.Status.Stopped"));
                return;
            }

            var exe = NvShaderCompilerService.FindCompiler();
            if (exe == null)
            {
                SetShaderCompileStatus(Loc.GetString("Settings.ShaderCompile.Status.CompilerNotFound"));
                return;
            }

            string resUtil = _window.ShaderPrecompileCombo.SelectedIndex switch
            {
                1 => "MIN",
                3 => "MAX",
                _ => "MED", // Medium, and Off (the NVIDIA App's own Compile now default)
            };

            ShowShaderCompileRunning(ours: true);
            ShaderCompileProgress? last = null;
            var progress = new Progress<ShaderCompileProgress>(p =>
            {
                last = p;
                if (_shaderCompileOurs) ShowShaderCompileProgress(p);
            });
            var started = DateTime.Now;
            int? code = await NvShaderCompilerService.RunAsync(exe, resUtil, _window.LifetimeToken, progress);
            if (_window.IsShuttingDown || !_shaderCompileOurs) return; // stopped with the button: already reported
            ShowShaderCompileIdle();

            var elapsed = DateTime.Now - started;
            string took = elapsed.TotalMinutes < 1
                ? Loc.GetString("Settings.ShaderCompile.UnderMinute")
                : Loc.GetString("Settings.ShaderCompile.Minutes", elapsed.TotalMinutes.ToString("F0"));
            if (code == 0 && last is { Total: > 0 })
                took += Loc.GetString(last.Total == 1 ? "Settings.ShaderCompile.AppsOne" : "Settings.ShaderCompile.AppsMany", last.Total);
            SetShaderCompileStatus(code switch
            {
                null => Loc.GetString("Settings.ShaderCompile.Status.StartFailed"),
                // It only rebuilds shaders games have already saved in the cache: an empty or fresh cache ends at once
                0 when elapsed.TotalSeconds < 5 => Loc.GetString("Settings.ShaderCompile.Status.NothingToCompile"),
                0 => Loc.GetString("Settings.ShaderCompile.Status.Done", $"{DateTime.Now:HH:mm}", took),
                _ => Loc.GetString("Settings.ShaderCompile.Status.ExitCode", code),
            });
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.ShaderCompileBtn_Click] {ex.Message}");
            ShowShaderCompileIdle();
        }
    }

    private void ShowShaderCompileProgress(ShaderCompileProgress p)
    {
        if (p.Total == 0) return; // no app caches found: keep the moving bar and the general text
        if (_shaderCompileBar != null)
        {
            _shaderCompileBar.IsIndeterminate = p.Fraction == null;
            if (p.Fraction is double f) _shaderCompileBar.Value = f * 100;
        }
        if (p.Done == 0) SetShaderCompileStatus(Loc.GetString(
            p.Total == 1 ? "Settings.ShaderCompile.Status.CompilingAppsOne" : "Settings.ShaderCompile.Status.CompilingAppsMany", p.Total));
        else SetShaderCompileStatus(Loc.GetString("Settings.ShaderCompile.Status.CompilingProgress",
            p.CurrentApp ?? Loc.GetString("Settings.ShaderCompile.Shaders"), p.Done, p.Total));
    }

    /// <summary>A run RHI didn't start was going when Settings opened: reset the button when it ends.</summary>
    private async Task WatchOtherShaderCompileAsync()
    {
        try
        {
            while (!_window.IsShuttingDown && !_shaderCompileOurs && await Task.Run(NvShaderCompilerService.IsRunning))
                await Task.Delay(3000, _window.LifetimeToken);
            if (!_window.IsShuttingDown && !_shaderCompileOurs) { ShowShaderCompileIdle(); SetShaderCompileStatus(null); }
        }
        catch (OperationCanceledException) { /* RHI closing */ }
    }

    private void ShowShaderCompileRunning(bool ours)
    {
        _shaderCompileOurs = ours;
        if (_shaderCompileBtn != null) _shaderCompileBtn.Content = Loc.GetString("Settings.ShaderCompile.StopCompiling");
        if (_shaderCompileBar != null)
        {
            _shaderCompileBar.IsIndeterminate = true; // until the first progress report (or for good without the log)
            _shaderCompileBar.Value = 0;
            _shaderCompileBar.Visibility = ours ? Visibility.Visible : Visibility.Collapsed;
        }
        SetShaderCompileStatus(Loc.GetString(ours
            ? "Settings.ShaderCompile.Status.RunningOurs"
            : "Settings.ShaderCompile.Status.RunningOther"));
    }

    private void ShowShaderCompileIdle()
    {
        _shaderCompileOurs = false;
        if (_shaderCompileBtn != null) _shaderCompileBtn.Content = ShaderCompileIdleText;
        if (_shaderCompileBar != null) _shaderCompileBar.Visibility = Visibility.Collapsed;
    }

    private void SetShaderCompileStatus(string? text)
    {
        if (_shaderCompileStatus == null) return;
        _shaderCompileStatus.Text = text ?? "";
        _shaderCompileStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
