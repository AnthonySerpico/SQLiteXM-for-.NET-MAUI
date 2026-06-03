using QueryGalleryDemo.Models;

namespace QueryGalleryDemo.Services;

/// <summary>
/// Service for managing navigation state between pages.
/// This avoids serialization issues with Shell navigation in Release mode.
/// </summary>
public static class NavigationService
{
    private static QueryExample? _selectedQuery;

    public static void SetSelectedQuery(QueryExample query)
    {
        _selectedQuery = query;
    }

    public static QueryExample? GetSelectedQuery()
    {
        var query = _selectedQuery;
        _selectedQuery = null; // Clear after reading
        return query;
    }
}
