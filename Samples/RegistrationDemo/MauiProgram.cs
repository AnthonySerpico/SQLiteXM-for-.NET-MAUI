using Microsoft.Extensions.Logging;
using RegistrationDemo.Models;
using RegistrationDemo.ViewModels;
using RegistrationDemo.Views;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace RegistrationDemo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Initialize SQLiteXM database
		Task.Run(InitializeDatabaseAsync).GetAwaiter().GetResult();

        // Register ViewModels and Views
        builder.Services.AddSingleton<WelcomeViewModel>();
		builder.Services.AddSingleton<WelcomePage>();

		builder.Services.AddTransient<EmailPasswordViewModel>();
		builder.Services.AddTransient<EmailPasswordPage>();

		builder.Services.AddTransient<PersonalInfoViewModel>();
		builder.Services.AddTransient<PersonalInfoPage>();

		builder.Services.AddTransient<PreferencesViewModel>();
		builder.Services.AddTransient<PreferencesPage>();

		builder.Services.AddTransient<ReviewViewModel>();
		builder.Services.AddTransient<ReviewPage>();

		builder.Services.AddTransient<HomeViewModel>();
		builder.Services.AddTransient<HomePage>();

		return builder.Build();
	}

	/// <summary>
	/// Initializes SQLiteXM database and registers entity schemas.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>About the suppression attributes:</strong>
	/// The IL2062 and IL2026 warnings are false positives when building for iOS with AOT compilation enabled.
	/// Visual Studio's static analyzer cannot determine across assembly boundaries that RegisterEntitiesAsync
	/// has [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] on its entityTypes parameter.
	/// </para>
	/// <para>
	/// <strong>Why this is safe:</strong>
	/// - Entity types are explicitly provided as typeof() compile-time constants (User, UserPreferences, RegistrationDraft)
	/// - RegisterEntitiesAsync's [DynamicallyAccessedMembers] attribute ensures the trimmer preserves all members
	/// - No dynamic type loading or reflection is used - all types are statically known
	/// </para>
	/// <para>
	/// <strong>For your own apps:</strong>
	/// You may see similar warnings when calling RegisterEntitiesAsync. This is expected and safe to suppress
	/// as long as you pass typeof() constants (not dynamically loaded types).
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "RegisterEntitiesAsync has [DynamicallyAccessedMembers] on its parameter. Types are statically known at compile time.")]
	[UnconditionalSuppressMessage("AOT", "IL2062:Value passed to parameter 'entityTypes' cannot be statically determined", Justification = "RegisterEntitiesAsync has [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] on its parameter. Entity types (User, UserPreferences, RegistrationDraft) are explicitly provided as typeof() constants.")]
	private static async Task InitializeDatabaseAsync()
	{
		try
		{
			// Load SQL statements file from Resources/Raw
			await using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

			// Initialize database with app data directory
			var options = new SxmDatabaseOptions
			{
				DatabaseFolderOverride = FileSystem.AppDataDirectory,
				ForeignKeys = true,
				JournalModeOption = SxmJournalMode.Wal
			};

			string whereAreYou = FileSystem.AppDataDirectory;

            await SxmDatabase.InitializeAsync(stream, options);

			// Register entity schemas
			await SxmDatabase.RegisterEntitiesAsync(
				typeof(User),
				typeof(UserPreferences),
				typeof(RegistrationDraft)
			);

			Console.WriteLine($"✅ Database initialized successfully in: {FileSystem.AppDataDirectory}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
			throw;
		}
	}
}
