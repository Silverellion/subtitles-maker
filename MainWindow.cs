using Avalonia.Controls;
using Avalonia.Media;
using System;
using subtitles_maker.Views.Home;
using subtitles_maker.Views.Models;

namespace subtitles_maker
{
    public partial class MainWindow : Window
    {
        private const double CollapsedWidth = 50;

        public MainWindow()
        {
            InitializeComponent();
            SetupSidebarHandler();
        }

        private void SetupSidebarHandler()
        {
            try
            {
                var sidebar = this.FindControl<Views.Sidebar.Sidebar>("AppSidebar");
                if (sidebar != null)
                {
                    sidebar.Width = CollapsedWidth;
                    sidebar.OnToggled += expanded =>
                    {
                        // sidebar animates itself. Keep hook for future behavior.
                    };

                    sidebar.OnHomeSelected += () =>
                    {
                        var mainContent = this.FindControl<ContentControl>("MainContent");
                        if (mainContent != null)
                            mainContent.Content = new HomeView();

                        var contentBorder = this.FindControl<Border>("ContentBorder");
                        if (contentBorder != null)
                            contentBorder.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    };

                    sidebar.OnModelsSelected += () =>
                    {
                        var mainContent = this.FindControl<ContentControl>("MainContent");
                        if (mainContent != null)
                            mainContent.Content = new ModelsView();

                        var contentBorder = this.FindControl<Border>("ContentBorder");
                        if (contentBorder != null)
                            contentBorder.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    };
                }
            }
            catch (Exception) { }
        }
    }
}