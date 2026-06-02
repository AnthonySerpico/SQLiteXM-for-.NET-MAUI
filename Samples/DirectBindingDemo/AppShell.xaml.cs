using DirectBindingDemo.Views;

namespace DirectBindingDemo;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("EmailPasswordPage", typeof(EmailPasswordPage));
        Routing.RegisterRoute("PersonalInfoPage", typeof(PersonalInfoPage));
        Routing.RegisterRoute("PreferencesPage", typeof(PreferencesPage));
        Routing.RegisterRoute("ReviewPage", typeof(ReviewPage));
        Routing.RegisterRoute("HomePage", typeof(HomePage));
    }
}
