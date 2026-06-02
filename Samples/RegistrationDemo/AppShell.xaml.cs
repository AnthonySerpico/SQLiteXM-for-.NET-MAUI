using RegistrationDemo.Views;

namespace RegistrationDemo;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register navigation routes
		Routing.RegisterRoute(nameof(EmailPasswordPage), typeof(EmailPasswordPage));
		Routing.RegisterRoute(nameof(PersonalInfoPage), typeof(PersonalInfoPage));
		Routing.RegisterRoute(nameof(PreferencesPage), typeof(PreferencesPage));
		Routing.RegisterRoute(nameof(ReviewPage), typeof(ReviewPage));
		Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
	}
}
