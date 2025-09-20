using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace subtitles_maker
{
    public partial class MainWindow : Window
    {
        private const double CollapsedWidth = 70;
        private const double ExpandedWidth = 240;

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
                var rootGrid = this.FindControl<Grid>("RootGrid");
                if (sidebar != null && rootGrid != null)
                {
                    rootGrid.ColumnDefinitions[0].Width = new GridLength(CollapsedWidth);
                    sidebar.OnToggled += expanded =>
                    {
                        rootGrid.ColumnDefinitions[0].Width = new GridLength(expanded ? ExpandedWidth : CollapsedWidth);
                    };
                }
            }
            catch (Exception) { }
        }
    }
}