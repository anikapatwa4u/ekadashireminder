using EkadashiReminder.Pages;

namespace EkadashiReminder
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(AddReminderPage), typeof(AddReminderPage));
        }
    }
}
