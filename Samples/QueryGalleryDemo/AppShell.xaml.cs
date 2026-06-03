using QueryGalleryDemo.Views;

namespace QueryGalleryDemo;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("QueryMenuPage", typeof(QueryMenuPage));
        Routing.RegisterRoute("QueryCategoryPage", typeof(QueryCategoryPage));
        Routing.RegisterRoute("QueryExecutionPage", typeof(QueryExecutionPage));
    }
}
