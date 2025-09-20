using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace subtitles_maker.Views.Sidebar
{
    public partial class Sidebar : UserControl
    {
        public event Action<bool>? OnToggled;
        private bool _expanded = false;
        private const double CollapsedWidth = 50;
        private const double ExpandedWidth = 150;

        public Sidebar()
        {
            InitializeComponent();
            Width = CollapsedWidth;
            SetLabelsOpacity(0);

            var menu = this.FindControl<Button>("MenuButton");
            if (menu != null)
                menu.Click += MenuButton_Click;
        }

        private void MenuButton_Click(object? sender, RoutedEventArgs e)
        {
            _expanded = !_expanded;
            Width = _expanded ? ExpandedWidth : CollapsedWidth;
            SetLabelsOpacity(_expanded ? 1 : 0);
            ToolTip.SetTip(MenuButton, _expanded ? "Minimize" : "Expand");
            OnToggled?.Invoke(_expanded);
        }

        private void SetLabelsOpacity(double opacity)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var menuLabel = this.FindControl<TextBlock>("MenuLabel");
                var homeLabel = this.FindControl<TextBlock>("HomeLabel");
                var modelsLabel = this.FindControl<TextBlock>("ModelsLabel");

                if (menuLabel != null)
                    menuLabel.Opacity = opacity;
                if (homeLabel != null)
                    homeLabel.Opacity = opacity;
                if (modelsLabel != null)
                    modelsLabel.Opacity = opacity;
            }, DispatcherPriority.Background);
        }
    }
}