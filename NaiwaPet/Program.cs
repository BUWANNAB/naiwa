using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace NaiwaPet;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        bool selfTest = args.Length > 0 && args[0] == "--self-test";
        using var single = new Mutex(true, "Local\\NaiwaPet.28379.v1", out bool first);
        if (!first && !selfTest) return;
        var app = new System.Windows.Application();
        app.DispatcherUnhandledException += (_, e) => {
            System.Windows.MessageBox.Show(e.Exception.Message, "奶蛙启动失败");
            e.Handled = true;
            app.Shutdown(1);
        };
        try {
            var window = new PetWindow(selfTest);
            if (selfTest) window.Loaded += async (_, _) => {
                int result = await window.RunSelfTest(args.Length > 1 ? args[1] : "qa");
                app.Shutdown(result);
            };
            app.Run(window);
        } catch (Exception error) {
            if (selfTest) {
                string folder = args.Length > 1 ? args[1] : "qa";
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "runtime-checks.json"), JsonSerializer.Serialize(new { ok = false, error = error.ToString() }));
            } else System.Windows.MessageBox.Show(error.Message, "奶蛙启动失败");
            Environment.ExitCode = 1;
        }
    }
}

public sealed class PetSettings
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public bool Muted { get; set; }
    public double Volume { get; set; } = 0.45;
    public bool Topmost { get; set; } = true;
}

public sealed class PetWindow : Window
{
    readonly Image sprite = new Image { Stretch = Stretch.Uniform, IsHitTestVisible = false };
    readonly Grid surface = new Grid { Background = Brushes.Transparent };
    readonly Dictionary<string, MediaPlayer> sounds = new();
    readonly Dictionary<string, string> audioStatuses = new();
    string activeSound = "laugh";
    MediaPlayer audio => sounds[activeSound];
    readonly Dictionary<string, BitmapImage[]> frames = new();
    readonly PetInteraction interaction = new PetInteraction();
    readonly DispatcherTimer animation = new DispatcherTimer();
    readonly DispatcherTimer finishLaugh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
    readonly DispatcherTimer singleClick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime) };
    readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaiwaPet");
    readonly bool testing;
    readonly PetSettings settings;
    readonly Forms.NotifyIcon? tray;
    readonly MenuItem muteItem = new MenuItem { Header = "暂时静音", IsCheckable = true, StaysOpenOnClick = true, Icon = MilkMenu.Icon("♫") };
    readonly ContextMenu menu;
    readonly MenuItem volumeMenu;
    string action = "idle";
    int frameIndex;
    double originLeft, originTop;
    bool doubleClickPress;
    int audioLoops;

    public PetWindow(bool selfTest)
    {
        testing = selfTest;
        Directory.CreateDirectory(dataDir);
        settings = selfTest ? new PetSettings { Muted = true } : ReadSettings();
        Title = "奶蛙 · 奶油菜单版";
        Width = 224; Height = 244;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = settings.Topmost;
        Left = settings.X ?? SystemParameters.WorkArea.Right - Width - 36;
        Top = settings.Y ?? SystemParameters.WorkArea.Bottom - Height - 20;
        ClampPosition();
        surface.Margin = new Thickness(4);
        surface.Children.Add(sprite);
        Content = surface;
        LoadFrames("idle", 6); LoadFrames("laugh", 8); LoadFrames("wave", 8); LoadFrames("sleep", 3);
        LoadFrames("punch", 8); LoadFrames("gossip", 8);
        animation.Tick += (_, _) => {
            frameIndex = (frameIndex + 1) % frames[action].Length;
            sprite.Source = frames[action][frameIndex];
        };
        finishLaugh.Tick += (_, _) => CompleteAction();
        singleClick.Tick += (_, _) => { singleClick.Stop(); BeginLaugh(); };
        SetAction("idle");
        LoadSound("laugh", "Assets/laugh.wav");
        foreach (string voice in new[] { "gaga", "gajiao", "andi", "ganba" }) LoadSound(voice, $"Assets/voices/{voice}.wav");
        ApplyVolume();
        muteItem.IsChecked = settings.Muted;
        muteItem.Click += (_, _) => SetMuted(muteItem.IsChecked);
        menu = MilkMenu.Create();
        MilkMenu.Section(menu, "奶蛙语音");
        menu.Items.Add(MilkMenu.Item("嘎嘎滴辣虾", "✦", (_, _) => BeginVoice("gaga"), "奶蛙出拳 · 双击也能触发"));
        menu.Items.Add(MilkMenu.Item("安迪", "♫", (_, _) => BeginVoice("andi"), "凑近一点，讲讲小八卦"));
        menu.Items.Add(MilkMenu.Item("噶脚塞", "➜", (_, _) => BeginVoice("gajiao"), "再来一套奶蛙拳"));
        menu.Items.Add(MilkMenu.Item("加油", "♡", (_, _) => BeginVoice("ganba"), "挥挥小手，给你打气"));
        menu.Items.Add(new Separator());
        var actions = new MenuItem { Header = "动作小剧场", Icon = MilkMenu.Icon("☻") };
        actions.Items.Add(MilkMenu.Item("捧腹大笑", "☺", (_, _) => BeginLaugh()));
        actions.Items.Add(MilkMenu.Item("安静待机", "☁", (_, _) => StopLaugh()));
        actions.Items.Add(MilkMenu.Item("挥挥小手", "♡", (_, _) => { StopLaugh(); SetAction("wave"); }));
        actions.Items.Add(MilkMenu.Item("奶蛙睡觉", "☾", (_, _) => { StopLaugh(); SetAction("sleep"); }));
        menu.Items.Add(actions);
        menu.Items.Add(muteItem);
        volumeMenu = new MenuItem { Header = "奶声音量", Icon = MilkMenu.Icon("♪") };
        foreach (int value in new[] { 20, 45, 75, 100 }) {
            int level = value;
            var item = new MenuItem { Header = $"{level}%", IsCheckable = true, IsChecked = Math.Abs(settings.Volume - level / 100.0) < 0.001, StaysOpenOnClick = true, Tag = level };
            item.Click += (_, _) => {
                settings.Volume = level / 100.0;
                foreach (MenuItem choice in volumeMenu.Items) choice.IsChecked = (int)choice.Tag == level;
                ApplyVolume(); SaveSettings();
            };
            volumeMenu.Items.Add(item);
        }
        menu.Items.Add(volumeMenu);
        var top = new MenuItem { Header = "一直陪在最前面", Icon = MilkMenu.Icon("⌂"), IsCheckable = true, IsChecked = Topmost, StaysOpenOnClick = true };
        top.Click += (_, _) => { Topmost = settings.Topmost = top.IsChecked; SaveSettings(); };
        menu.Items.Add(top);
        menu.Items.Add(new Separator());
        var exit = MilkMenu.Item("退出奶蛙 · 回窝啦", "⏻", (_, _) => Close());
        exit.Foreground = new SolidColorBrush(Color.FromRgb(163, 100, 86));
        menu.Items.Add(exit);
        surface.ContextMenu = menu;
        surface.MouseLeftButtonDown += OnPress;
        surface.MouseMove += OnMove;
        surface.MouseLeftButtonUp += OnRelease;
        surface.LostMouseCapture += (_, _) => {
            if (interaction.Pressed) StopLaugh();
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { StopLaugh(); surface.ReleaseMouseCapture(); } };
        {
            using var loadedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            tray = new Forms.NotifyIcon { Icon = (System.Drawing.Icon)(loadedIcon ?? System.Drawing.SystemIcons.Application).Clone(), Text = "奶蛙：拖动大笑 · 双击嘎嘎滴辣虾", Visible = !selfTest };
            var trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add("把奶蛙叫回来", null, (_, _) => { Left = SystemParameters.WorkArea.Right - Width - 36; Top = SystemParameters.WorkArea.Bottom - Height - 20; Show(); Activate(); });
            trayMenu.Items.Add("静音 / 恢复声音", null, (_, _) => SetMuted(!settings.Muted));
            trayMenu.Items.Add("退出奶蛙 · 回窝啦", null, (_, _) => Close());
            MilkMenu.StyleTray(trayMenu);
            tray.ContextMenuStrip = trayMenu;
        }
        Closed += (_, _) => { SaveSettings(); animation.Stop(); finishLaugh.Stop(); singleClick.Stop(); foreach (var player in sounds.Values) player.Close(); if (tray != null) { tray.Icon?.Dispose(); tray.Dispose(); } };
    }

    void LoadSound(string name, string resource)
    {
        using var source = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/" + resource)).Stream;
        using var buffer = new MemoryStream(); source.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(bytes))[..12];
        string file = Path.Combine(dataDir, $"{name}-{hash}.wav");
        if (!File.Exists(file)) File.WriteAllBytes(file, bytes);
        var player = new MediaPlayer();
        sounds.Add(name, player); audioStatuses.Add(name, "loading");
        player.MediaOpened += (_, _) => audioStatuses[name] = "opened";
        player.MediaFailed += (_, e) => audioStatuses[name] = "failed: " + e.ErrorException.Message;
        player.MediaEnded += (_, _) => {
            if (activeSound != name) return;
            if (interaction.Dragging && name == "laugh") { audioLoops++; player.Position = TimeSpan.Zero; player.Play(); }
            else CompleteAction();
        };
        player.Open(new Uri(file));
    }

    void LoadFrames(string state, int count)
    {
        var result = new BitmapImage[count];
        for (int i = 0; i < count; i++) {
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri($"pack://application:,,,/Assets/{state}/{i:00}.png");
            image.EndInit(); image.Freeze(); result[i] = image;
        }
        frames.Add(state, result);
    }

    void SetAction(string next)
    {
        action = next; frameIndex = 0; sprite.Source = frames[next][0];
        animation.Interval = TimeSpan.FromSeconds(1.0 / (next == "sleep" ? 1 : next == "laugh" || next == "wave" ? 11 : 8));
        animation.Start();
    }

    void BeginLaugh()
    {
        singleClick.Stop(); StopAllAudio(); activeSound = "laugh";
        interaction.Laugh();
        SetAction("laugh"); audio.Position = TimeSpan.Zero; ApplyVolume(); audio.Play();
        finishLaugh.Stop();
        if (!interaction.Dragging) finishLaugh.Start();
    }

    void StopLaugh()
    {
        interaction.Stop(); finishLaugh.Stop(); singleClick.Stop(); StopAllAudio(); Cursor = Cursors.Arrow; SetAction("idle");
    }

    void CompleteAction()
    {
        interaction.FinishLaugh(); finishLaugh.Stop(); StopAllAudio(); SetAction("idle");
    }

    void StopAllAudio()
    {
        foreach (var player in sounds.Values) player.Stop();
    }

    void BeginVoice(string voice)
    {
        if (!sounds.ContainsKey(voice)) return;
        singleClick.Stop(); finishLaugh.Stop(); StopAllAudio();
        interaction.Stop(); activeSound = voice;
        SetAction(voice == "andi" ? "gossip" : voice == "ganba" ? "wave" : "punch");
        ApplyVolume(); audio.Play(); finishLaugh.Start();
    }

    void QueueClick(bool doubleClick)
    {
        singleClick.Stop();
        if (doubleClick) BeginVoice("gaga");
        else singleClick.Start();
    }

    void SetMuted(bool muted) { settings.Muted = muted; muteItem.IsChecked = muted; ApplyVolume(); SaveSettings(); }
    void ApplyVolume() { foreach (var player in sounds.Values) player.Volume = settings.Muted ? 0 : Math.Clamp(settings.Volume, 0, 1); }

    void OnPress(object sender, MouseButtonEventArgs e)
    {
        singleClick.Stop();
        doubleClickPress = e.ClickCount > 0 && e.ClickCount % 2 == 0;
        var screen = PointToScreen(e.GetPosition(this));
        interaction.Press(screen.X, screen.Y); originLeft = Left; originTop = Top;
        surface.CaptureMouse(); e.Handled = true;
    }

    void OnMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!interaction.Pressed) return;
        if (e.LeftButton != MouseButtonState.Pressed) { StopLaugh(); surface.ReleaseMouseCapture(); return; }
        var point = PointToScreen(e.GetPosition(this));
        MovePointer(point.X, point.Y);
    }

    void MovePointer(double x, double y)
    {
        if (interaction.Move(x, y)) { Cursor = Cursors.SizeAll; BeginLaugh(); }
        if (!interaction.Dragging) return;
        var transform = PresentationSource.FromVisual(this)!.CompositionTarget.TransformFromDevice;
        var delta = transform.Transform(new Vector(x - interaction.StartX, y - interaction.StartY));
        Left = originLeft + delta.X; Top = originTop + delta.Y;
    }

    void OnRelease(object sender, MouseButtonEventArgs e)
    {
        if (!interaction.Pressed) return;
        bool click = interaction.Release();
        surface.ReleaseMouseCapture(); Cursor = Cursors.Arrow;
        if (click) QueueClick(doubleClickPress); else StopLaugh();
        ClampPosition(); SaveSettings(); e.Handled = true;
    }

    void ClampPosition()
    {
        Left = Math.Clamp(Left, SystemParameters.VirtualScreenLeft, Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
        Top = Math.Clamp(Top, SystemParameters.VirtualScreenTop, Math.Max(SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
    }

    PetSettings ReadSettings()
    {
        try {
            var value = JsonSerializer.Deserialize<PetSettings>(File.ReadAllText(Path.Combine(dataDir, "settings.json"))) ?? new PetSettings();
            if (value.X.HasValue && !double.IsFinite(value.X.Value)) value.X = null;
            if (value.Y.HasValue && !double.IsFinite(value.Y.Value)) value.Y = null;
            value.Volume = double.IsFinite(value.Volume) ? Math.Clamp(value.Volume, 0, 1) : 0.45;
            return value;
        } catch (IOException) { return new PetSettings(); } catch (UnauthorizedAccessException) { return new PetSettings(); } catch (JsonException) { return new PetSettings(); }
    }

    void SaveSettings()
    {
        if (testing) return;
        settings.X = Left; settings.Y = Top;
        try { File.WriteAllText(Path.Combine(dataDir, "settings.json"), JsonSerializer.Serialize(settings)); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public async Task<int> RunSelfTest(string folder)
    {
        Directory.CreateDirectory(folder);
        var checks = new Dictionary<string, object>();
        try {
            for (int n = 0; n < 100 && audioStatuses.ContainsValue("loading"); n++) await Task.Delay(100);
            foreach (var status in audioStatuses) if (status.Value != "opened") throw new Exception($"Audio decode failed ({status.Key}): {status.Value}");
            checks["allFiveSoundsDecoded"] = sounds.Count == 5;
            checks["audioDecoded"] = audio.NaturalDuration.HasTimeSpan && audio.NaturalDuration.TimeSpan.TotalSeconds > 0;
            interaction.Press(100, 100); originLeft = Left; originTop = Top;
            MovePointer(160, 130);
            if (!interaction.Dragging || action != "laugh") throw new Exception("Drag did not enter laugh state");
            checks["dragStartsLaugh"] = true;
            checks["windowFollowsDrag"] = Left > originLeft && Top > originTop;
            await Task.Delay(1200);
            checks["audioPlaybackAdvances"] = audio.Position.TotalMilliseconds > 100;
            checks["animationAdvances"] = frameIndex > 0;
            Capture(Path.Combine(folder, "drag-laugh.png"));
            audio.Position = audio.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(300);
            await Task.Delay(1600);
            checks["audioLoopsDuringDrag"] = audioLoops > 0 && interaction.Dragging && action == "laugh";
            SetMuted(true); checks["mute"] = audio.Volume == 0;
            SetMuted(false); checks["unmute"] = audio.Volume > 0;
            SetMuted(true);
            OnRelease(this, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
            checks["releaseRestoresIdle"] = action == "idle" && !interaction.Dragging && audio.Position == TimeSpan.Zero;
            Capture(Path.Combine(folder, "idle.png"));
            BeginLaugh(); audio.Position = audio.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(300);
            await Task.Delay(1600);
            checks["clickCompletesToIdle"] = action == "idle";
            interaction.Press(100, 100); MovePointer(140, 140);
            surface.RaiseEvent(new System.Windows.Input.MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
            await Task.Delay(150);
            TimeSpan stoppedAt = audio.Position;
            await Task.Delay(200);
            // WPF may retain one decoded packet (about 20 ms) after Stop().
            // A stationary clock at the beginning confirms playback actually stopped.
            checks["captureLossStopsLaugh"] = !interaction.Pressed && action == "idle" && audio.Position.TotalMilliseconds < 50 && Math.Abs((audio.Position - stoppedAt).TotalMilliseconds) < 1;
            QueueClick(false);
            await Task.Delay(Math.Max(20, Forms.SystemInformation.DoubleClickTime / 3));
            checks["singleClickWaitsForDoubleClick"] = action == "idle";
            QueueClick(true);
            await Task.Delay(Forms.SystemInformation.DoubleClickTime + 100);
            checks["doubleClickUsesGagaWithoutLaughOverlap"] = activeSound == "gaga" && action == "punch" && sounds["laugh"].Position == TimeSpan.Zero;
            StopLaugh();
            QueueClick(false);
            await Task.Delay(Forms.SystemInformation.DoubleClickTime + 150);
            checks["singleClickStillLaughs"] = activeSound == "laugh" && action == "laugh";
            StopLaugh();
            foreach (string voice in new[] { "gaga", "gajiao", "andi", "ganba" }) {
                BeginVoice(voice);
                await Task.Delay(250);
                checks[voice + "PlaybackAdvances"] = audio.Position.TotalMilliseconds > 80;
                checks[voice + "CorrectAction"] = action == (voice == "andi" ? "gossip" : voice == "ganba" ? "wave" : "punch");
                SetMuted(false); checks[voice + "Volume"] = Math.Abs(audio.Volume - settings.Volume) < 0.001;
                SetMuted(true); checks[voice + "Muted"] = audio.Volume == 0;
                Capture(Path.Combine(folder, voice + ".png"));
                audio.Position = audio.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(150);
                await Task.Delay(1000);
                checks[voice + "CompletesToIdle"] = action == "idle";
            }
            BeginVoice("andi"); await Task.Delay(200);
            interaction.Press(100, 100); originLeft = Left; originTop = Top; MovePointer(150, 150);
            checks["dragInterruptsVoiceWithoutOverlap"] = activeSound == "laugh" && action == "laugh" && sounds["andi"].Position == TimeSpan.Zero;
            StopLaugh();
            QueueClick(false); StopLaugh();
            await Task.Delay(Forms.SystemInformation.DoubleClickTime + 150);
            checks["cancelClearsPendingClick"] = action == "idle";
            BeginVoice("andi"); interaction.Press(100, 100); originLeft = Left; originTop = Top;
            audio.Position = audio.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(150);
            await Task.Delay(1000);
            checks["voiceCompletionPreservesHeldPress"] = interaction.Pressed && action == "idle";
            MovePointer(145, 145);
            checks["heldPressCanDragAfterVoiceCompletion"] = interaction.Dragging && action == "laugh";
            StopLaugh();
            menu.PlacementTarget = surface; menu.IsOpen = true;
            await Task.Delay(250); menu.UpdateLayout();
            CaptureElement(menu, Path.Combine(folder, "milk-menu.png"));
            volumeMenu.IsSubmenuOpen = true;
            await Task.Delay(200);
            var volumePopup = (System.Windows.Controls.Primitives.Popup)volumeMenu.Template.FindName("PART_Popup", volumeMenu);
            CaptureElement((FrameworkElement)volumePopup.Child, Path.Combine(folder, "milk-volume.png"));
            var volumeChoice = (MenuItem)volumeMenu.Items[2];
            volumeChoice.IsChecked = true; volumeChoice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            checks["menuVolumeChoiceWorks"] = Math.Abs(settings.Volume - 0.75) < 0.001 && volumeChoice.IsChecked;
            volumeMenu.IsSubmenuOpen = false;
            muteItem.IsChecked = false; muteItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            checks["menuMuteChoiceWorks"] = !settings.Muted && audio.Volume > 0;
            SetMuted(true);
            ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Task.Delay(250);
            checks["menuVoiceChoiceWorks"] = activeSound == "gaga" && action == "punch";
            StopLaugh(); menu.IsOpen = false;
            foreach (var entry in checks) if (entry.Value is bool passed && !passed) throw new Exception("Check failed: " + entry.Key);
            checks["ok"] = true;
            File.WriteAllText(Path.Combine(folder, "runtime-checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        } catch (Exception ex) {
            checks["ok"] = false; checks["error"] = ex.ToString();
            File.WriteAllText(Path.Combine(folder, "runtime-checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }

    void Capture(string file)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }

    static void CaptureElement(FrameworkElement element, string file)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }
}
