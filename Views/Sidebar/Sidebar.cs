using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace subtitles_maker.Views.Sidebar
{
    public partial class Sidebar : UserControl
    {
        private const double CollapsedWidth = 50;
        private const double ExpandedWidth = 150;

        public event Action<bool>? OnToggled;

        public bool IsExpanded { get; private set; } = false;

        public Sidebar()
        {
            InitializeComponent();
            MenuButton.Click += MenuButton_Click;
            UpdateVisualState();
        }

        private void MenuButton_Click(object? sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
            Width = IsExpanded ? ExpandedWidth : CollapsedWidth;
            UpdateVisualState();
            OnToggled?.Invoke(IsExpanded);
        }

        private void UpdateVisualState()
        {
            var labelOpacity = IsExpanded ? 1.0 : 0.0;
            HomeLabel.Opacity = labelOpacity;
            ModelsLabel.Opacity = labelOpacity;
        }
    }
}