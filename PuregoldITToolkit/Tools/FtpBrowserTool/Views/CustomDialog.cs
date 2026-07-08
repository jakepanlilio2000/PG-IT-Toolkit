using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Views
{
    public class CustomDialog : Window
    {
        public string InputText { get; private set; }
        public bool IsConfirmed { get; private set; } = false;

        public CustomDialog(string title, string message, string defaultInput = null, bool isConfirm = false, bool isInput = false)
        {
            Title = title;
            Width = 420;
            Height = isInput ? 240 : 200;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;

            // Dynamically fetch your app's theme resources
            var bgCard = Application.Current.TryFindResource("BgCard") as Brush ?? Brushes.White;
            var borderLight = Application.Current.TryFindResource("BorderLight") as Brush ?? Brushes.LightGray;
            var textPrimary = Application.Current.TryFindResource("TextPrimary") as Brush ?? Brushes.Black;
            var textSecondary = Application.Current.TryFindResource("TextSecondary") as Brush ?? Brushes.Gray;
            var brandPrimary = Application.Current.TryFindResource("BrandPrimary") as Brush ?? Brushes.DodgerBlue;
            var bgHighlight = Application.Current.TryFindResource("BgHighlight") as Brush ?? Brushes.LightGray;

            var mainBorder = new Border
            {
                Background = bgCard,
                CornerRadius = new CornerRadius(8),
                BorderBrush = borderLight,
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 20, Opacity = 0.2, ShadowDepth = 5, Color = Colors.Black }
            };

            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleTb = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = textPrimary,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(titleTb, 0);
            grid.Children.Add(titleTb);

            var msgTb = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textSecondary,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(msgTb, 1);
            grid.Children.Add(msgTb);

            int nextRow = 2;
            TextBox inputTb = null;
            if (isInput)
            {
                inputTb = new TextBox
                {
                    Text = defaultInput ?? "",
                    Padding = new Thickness(10),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 16),
                    BorderBrush = borderLight,
                    BorderThickness = new Thickness(1),
                    Background = Brushes.White
                };
                Grid.SetRow(inputTb, 2);
                grid.Children.Add(inputTb);
                nextRow = 3;
            }

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            if (isConfirm || isInput)
            {
                var btnCancel = new Button
                {
                    Content = isConfirm ? "No" : "Cancel",
                    Width = 90,
                    Padding = new Thickness(10, 8, 10, 8),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Background = bgHighlight,
                    Foreground = textPrimary,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
                btnPanel.Children.Add(btnCancel);
            }

            var btnOk = new Button
            {
                Content = isConfirm ? "Yes" : "OK",
                Width = 90,
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Background = brandPrimary,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnOk.Click += (s, e) => {
                IsConfirmed = true;
                if (isInput) InputText = inputTb.Text;
                DialogResult = true;
                Close();
            };
            btnPanel.Children.Add(btnOk);

            Grid.SetRow(btnPanel, nextRow);
            grid.Children.Add(btnPanel);

            mainBorder.Child = grid;
            Content = mainBorder;

            if (isInput) Loaded += (s, e) => { inputTb.Focus(); inputTb.SelectAll(); };
            else Loaded += (s, e) => { btnOk.Focus(); };
        }

        public static bool ShowConfirm(string title, string message)
        {
            var dlg = new CustomDialog(title, message, null, true, false);
            return dlg.ShowDialog() == true;
        }

        public static string ShowInput(string title, string message, string defaultText = "")
        {
            var dlg = new CustomDialog(title, message, defaultText, false, true);
            return dlg.ShowDialog() == true ? dlg.InputText : null;
        }

        public static void ShowAlert(string title, string message)
        {
            var dlg = new CustomDialog(title, message, null, false, false);
            dlg.ShowDialog();
        }
    }
}