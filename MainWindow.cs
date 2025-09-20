using Avalonia.Controls;
using System;

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
                }
            }
            catch (Exception) { }
        }
    }
}