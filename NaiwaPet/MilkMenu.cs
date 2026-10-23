using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace NaiwaPet;

public static class MilkMenu
{
    public static ContextMenu Create()
    {
        var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/MilkMenu.xaml") };
        return new ContextMenu {
            Resources = resources,
            Style = (Style)resources["MilkContextMenu"],
            MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 24)
        };
    }

    public static void Section(ContextMenu menu, string text)
    {
        menu.Items.Add(new MenuItem { Header = text, IsEnabled = false, Style = (Style)menu.Resources["MilkSection"] });
    }

    public static MenuItem Item(string text, string icon, RoutedEventHandler handler, string? caption = null)
    {
        object header = text;
        if (caption != null) {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium });
            stack.Children.Add(new TextBlock { Text = caption, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(158, 134, 83)), Margin = new Thickness(0, 3, 0, 0) });
            header = stack;
        }
        var item = new MenuItem { Header = header, Icon = Icon(icon) };
        item.Click += handler;
        return item;
    }

    public static TextBlock Icon(string text) => new TextBlock {
        Text = text, FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 16,
        Foreground = new SolidColorBrush(Color.FromRgb(169, 134, 52)), VerticalAlignment = VerticalAlignment.Center
    };

    public static void StyleTray(Forms.ContextMenuStrip menu)
    {
        menu.Font = new Drawing.Font("Microsoft YaHei UI", 9.5f);
        menu.ForeColor = Drawing.Color.FromArgb(96, 74, 37);
        menu.BackColor = Drawing.Color.FromArgb(255, 252, 241);
        menu.ShowImageMargin = false;
        menu.Padding = new Forms.Padding(6);
        menu.Renderer = new Forms.ToolStripProfessionalRenderer(new MilkColors()) { RoundedEdges = true };
        foreach (Forms.ToolStripItem item in menu.Items) item.Padding = new Forms.Padding(8, 6, 8, 6);
    }

    sealed class MilkColors : Forms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(255, 252, 241);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(234, 214, 155);
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(240, 212, 127);
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(255, 240, 181);
        public override Drawing.Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Drawing.Color MenuItemSelectedGradientEnd => MenuItemSelected;
    }
}
