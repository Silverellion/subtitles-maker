using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using System;

namespace subtitles_maker.Views.Sidebar
{
    public partial class SidebarView : UserControl
    {
        public event Action<bool>? OnToggled;
        public event Action? OnHomeSelected;
        public event Action? OnModelsSelected;

        private bool _expanded = false;
        private const double CollapsedWidth = 50;
        private const double ExpandedWidth = 200;
        public SidebarView()
        {
            InitializeComponent();
            Width = CollapsedWidth;
            SetLabelsOpacity(0);

            var menu = this.FindControl<Button>("MenuButton");
            if (menu != null)
                menu.Click += MenuButton_Click;

            var home = this.FindControl<Button>("HomeButton");
            if (home != null)
                home.Click += HomeButton_Click;

            var models = this.FindControl<Button>("ModelsButton");
            if (models != null)
                models.Click += ModelsButton_Click;

            SelectHome();
        }

        private void MenuButton_Click(object? sender, RoutedEventArgs e)
        {
            _expanded = !_expanded;
            Width = _expanded ? ExpandedWidth : CollapsedWidth;
            SetLabelsOpacity(_expanded ? 1 : 0);
            ToolTip.SetTip(MenuButton, _expanded ? "Minimize" : "Expand");
            OnToggled?.Invoke(_expanded);
        }

        private void HomeButton_Click(object? sender, RoutedEventArgs e)
        {
            SelectHome();
            OnHomeSelected?.Invoke();
        }

        private void ModelsButton_Click(object? sender, RoutedEventArgs e)
        {
            SelectModels();
            OnModelsSelected?.Invoke();
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

        private void SelectHome()
        {
            UpdateSelection("HomeButton");
        }

        private void SelectModels()
        {
            UpdateSelection("ModelsButton");
        }

        private void UpdateSelection(string selectedButtonName)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var home = this.FindControl<Button>("HomeButton");
                var models = this.FindControl<Button>("ModelsButton");
                var menu = this.FindControl<Button>("MenuButton");

                var selectedBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                var transparentBrush = Brushes.Transparent;

                if (home != null)
                    home.Background = selectedButtonName == "HomeButton" ? selectedBrush : transparentBrush;
                if (models != null)
                    models.Background = selectedButtonName == "ModelsButton" ? selectedBrush : transparentBrush;
                if (menu != null)
                    menu.Background = transparentBrush;
            }, DispatcherPriority.Background);
        }
    }
}