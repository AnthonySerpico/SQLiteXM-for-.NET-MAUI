using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Services;

/// <summary>
/// Service that seeds the Chinook database with realistic test data.
/// Generates approximately 25,000 records across all tables on first run.
/// </summary>
public class DatabaseSeeder
{
    private readonly Random _random = new();
    private const string SEED_KEY = "DatabaseSeeded";

    // Sample data arrays
    private readonly string[] _genreNames = new[]
    {
        "Rock", "Jazz", "Metal", "Alternative & Punk", "Classical", "Blues", "Latin",
        "Reggae", "Pop", "Soundtrack", "Bossa Nova", "Easy Listening", "Heavy Metal",
        "R&B/Soul", "Electronica/Dance", "World", "Hip Hop/Rap", "Science Fiction",
        "TV Shows", "Sci Fi & Fantasy", "Drama", "Comedy", "Alternative", "Opera",
        "Hip-Hop"
    };

    private readonly string[] _mediaTypeNames = new[]
    {
        "MPEG audio file", "Protected AAC audio file", "Protected MPEG-4 video file",
        "Purchased AAC audio file", "AAC audio file", "MPEG-4 video file",
        "MP3 audio file", "WAV audio file", "FLAC audio file", "OGG audio file"
    };

    private readonly string[] _artistNames = new[]
    {
        "AC/DC", "Accept", "Aerosmith", "Alanis Morissette", "Alice In Chains",
        "Antônio Carlos Jobim", "Apocalyptica", "Audioslave", "BackBeat",
        "Billy Cobham", "Black Label Society", "Black Sabbath", "Body Count",
        "Bruce Dickinson", "Buddy Guy", "Caetano Veloso", "Chico Buarque",
        "Chico Science & Nação Zumbi", "Cidade Negra", "Cláudio Zoli",
        "Various Artists", "Led Zeppelin", "Frank Zappa & The Mothers", "Creedence Clearwater Revival"
    };

    private readonly string[] _firstNames = new[]
    {
        "John", "Jane", "Michael", "Sarah", "David", "Emily", "Robert", "Lisa",
        "William", "Jessica", "James", "Ashley", "Richard", "Amanda", "Joseph",
        "Melissa", "Thomas", "Jennifer", "Charles", "Stephanie", "Christopher",
        "Nicole", "Daniel", "Elizabeth", "Matthew", "Rebecca", "Anthony", "Laura",
        "Mark", "Kimberly", "Donald", "Michelle", "Steven", "Amy", "Paul",
        "Angela", "Andrew", "Heather", "Joshua", "Pamela"
    };

    private readonly string[] _lastNames = new[]
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller",
        "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez",
        "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Lee", "Thompson", "White", "Harris", "Sanchez", "Clark", "Lewis",
        "Robinson", "Walker", "Young", "Allen", "King", "Wright", "Scott"
    };

    private readonly string[] _cities = new[]
    {
        "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia",
        "San Antonio", "San Diego", "Dallas", "San Jose", "Austin", "Jacksonville",
        "Fort Worth", "Columbus", "Charlotte", "San Francisco", "Indianapolis",
        "Seattle", "Denver", "Boston", "Nashville", "Baltimore", "Portland",
        "Las Vegas", "Detroit", "Memphis", "Louisville", "Milwaukee", "Albuquerque",
        "Tucson", "Fresno", "Sacramento", "Kansas City", "Atlanta", "Miami"
    };

    private readonly string[] _countries = new[]
    {
        "USA", "Canada", "Brazil", "France", "Germany", "Italy", "Spain",
        "United Kingdom", "Australia", "Japan", "India", "Mexico", "Argentina",
        "Sweden", "Norway", "Finland", "Netherlands", "Belgium", "Switzerland"
    };

    private readonly string[] _trackTitles = new[]
    {
        "Imagine", "Stairway to Heaven", "Bohemian Rhapsody", "Hotel California",
        "Sweet Child O' Mine", "Smells Like Teen Spirit", "Yesterday", "Hey Jude",
        "Come Together", "Let It Be", "Purple Haze", "Born to Run", "Thunder Road",
        "The River", "Dancing in the Dark", "Born in the U.S.A.", "Glory Days",
        "Tunnel of Love", "One Step Up", "Streets of Philadelphia", "The Ghost of Tom Joad"
    };

    public async Task<bool> IsDatabaseSeededAsync()
    {
        return Preferences.Get(SEED_KEY, false);
    }

    /// <summary>
    /// Checks if seeding is needed by verifying both the preference flag and actual data existence.
    /// This handles cases where the database file was manually deleted but the preference wasn't cleared.
    /// </summary>
    public Task<bool> CheckIfSeedingNeededAsync()
    {
        System.Diagnostics.Debug.WriteLine("DatabaseSeeder: CheckIfSeedingNeededAsync START");

        // First check the preference flag
        var isSeededPref = Preferences.Get(SEED_KEY, false);
        System.Diagnostics.Debug.WriteLine($"DatabaseSeeder: Preference flag is {isSeededPref}");

        if (!isSeededPref)
        {
            // Preference says not seeded, so we need seeding
            System.Diagnostics.Debug.WriteLine("DatabaseSeeder: Preference says not seeded, needs seeding");
            return Task.FromResult(true);
        }

        // Preference says it's seeded, verify the database file actually exists
        try
        {
            System.Diagnostics.Debug.WriteLine("DatabaseSeeder: Checking if database file exists...");

            // Get the database file path
            var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SQLiteXM");
            var dbPath = Path.Combine(dbFolder, "Chinook.db");

            System.Diagnostics.Debug.WriteLine($"DatabaseSeeder: Checking path: {dbPath}");

            if (!File.Exists(dbPath))
            {
                // Database file doesn't exist but preference says it's seeded - clear stale preference
                System.Diagnostics.Debug.WriteLine("DatabaseSeeder: Database file not found, clearing preference and needs seeding");
                Preferences.Remove(SEED_KEY);
                return Task.FromResult(true);
            }

            // File exists and preference is set, assume database is good
            System.Diagnostics.Debug.WriteLine("DatabaseSeeder: Database file exists and preference is set, no seeding needed");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            // Error checking file - assume we need seeding to be safe
            System.Diagnostics.Debug.WriteLine($"DatabaseSeeder: Exception checking database file: {ex.Message}");
            Preferences.Remove(SEED_KEY);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Registers all entity types with SQLiteXM. Must be called before querying.
    /// </summary>
    public async Task RegisterEntitiesAsync()
    {
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(Genre), typeof(MediaType), typeof(Artist), typeof(Album), typeof(Track),
            typeof(Playlist), typeof(PlaylistTrack), typeof(Employee), typeof(Customer),
            typeof(Invoice), typeof(InvoiceLine));
    }

    public async Task SeedDatabaseAsync(IProgress<string>? progress = null)
    {
        // Convert to the new progress type
        Action<(string status, double progress)>? newProgress = null;
        if (progress != null)
        {
            newProgress = update => progress.Report(update.status);
        }
        await SeedDatabaseAsync(newProgress);
    }

    public async Task SeedDatabaseAsync(Action<(string status, double progress)>? progress = null)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"DatabaseSeeder: Starting SeedDatabaseAsync, progress is {(progress == null ? "null" : "not null")}");

            const int totalSteps = 11;  // Fixed: Was 10, but there are 11 seeding steps
            int currentStep = 0;

            async Task ReportProgressAsync(string status)
            {
                currentStep++;
                double progressValue = (double)currentStep / totalSteps;
                System.Diagnostics.Debug.WriteLine($"DatabaseSeeder: Reporting progress - Step {currentStep}/{totalSteps} ({progressValue:P0}) - {status}");
                progress?.Invoke((status, progressValue));

                // Give UI thread time to process the update
                await Task.Delay(150);
            }

            progress?.Invoke(("Starting database seeding...", 0.0));
            await Task.Delay(200); // Initial delay to ensure UI is ready

            // Register all entities with SQLiteXM
            await RegisterEntitiesAsync();

            // Check if we have partial data (interrupted seeding)
            // If tables exist but are empty or incomplete, we'll detect it in individual seed methods

            // Seed reference data first
            await ReportProgressAsync("Seeding genres...");
            var genres = await SeedGenresAsync(null);

            await ReportProgressAsync("Seeding media types...");
            var mediaTypes = await SeedMediaTypesAsync(null);

            await ReportProgressAsync("Seeding artists...");
            var artists = await SeedArtistsAsync(null);

            // Seed albums and tracks (large datasets)
            await ReportProgressAsync("Seeding albums...");
            var albums = await SeedAlbumsAsync(artists, null);

            await ReportProgressAsync("Seeding tracks...");
            var tracks = await SeedTracksAsync(albums, mediaTypes, genres, null);

            // Seed playlists and many-to-many relationships
            await ReportProgressAsync("Seeding playlists...");
            var playlists = await SeedPlaylistsAsync(null);

            await ReportProgressAsync("Linking playlists with tracks...");
            await SeedPlaylistTracksAsync(playlists, tracks, null);

            // Seed customers and employees
            await ReportProgressAsync("Seeding employees...");
            var employees = await SeedEmployeesAsync(null);

            await ReportProgressAsync("Seeding customers...");
            var customers = await SeedCustomersAsync(employees, null);

            // Seed invoices and invoice lines
            await ReportProgressAsync("Seeding invoices...");
            var invoices = await SeedInvoicesAsync(customers, null);

            await ReportProgressAsync("Seeding invoice lines...");
            await SeedInvoiceLinesAsync(invoices, tracks, null);

            // Mark as seeded
            Preferences.Set(SEED_KEY, true);

            progress?.Invoke(("Database seeding completed successfully!", 1.0));
        }
        catch (Exception ex)
        {
            progress?.Invoke(($"Error seeding database: {ex.Message}", 0.0));
            throw;
        }
    }

    private async Task<List<Genre>> SeedGenresAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding genres...");
        var genres = new List<Genre>();

        // Check if genres already exist
        await using (var context = new SxmTransaction("Chinook"))
        {
            var existingGenres = await context.GetTable<Genre>().ToListAsync();
            if (existingGenres.Count > 0)
            {
                progress?.Report($"Genres already exist ({existingGenres.Count}), skipping...");
                return existingGenres;
            }

            foreach (var name in _genreNames)
            {
                var genre = new Genre { Name = name };
                await genre.SaveAsync();
                genres.Add(genre);
            }
        }
        return genres;
    }

    private async Task<List<MediaType>> SeedMediaTypesAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding media types...");
        var mediaTypes = new List<MediaType>();

        await using (var context = new SxmTransaction("Chinook"))
        {
            var existingMediaTypes = await context.GetTable<MediaType>().ToListAsync();
            if (existingMediaTypes.Count > 0)
            {
                progress?.Report($"Media types already exist ({existingMediaTypes.Count}), skipping...");
                return existingMediaTypes;
            }

            foreach (var name in _mediaTypeNames)
            {
                var mediaType = new MediaType { Name = name };
                await mediaType.SaveAsync();
                mediaTypes.Add(mediaType);
            }

            return mediaTypes;
        }
    }

    private async Task<List<Artist>> SeedArtistsAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding 200 artists...");
        var artists = new List<Artist>();

        // Check if artists already exist
        await using (var context = new SxmTransaction("Chinook"))
        {
            var existingArtists = await context.GetTable<Artist>().ToListAsync();
            if (existingArtists.Count > 0)
            {
                progress?.Report($"Artists already exist ({existingArtists.Count}), skipping...");
                return existingArtists;
            }

            // Add known artists
            foreach (var name in _artistNames)
            {
                var artist = new Artist { Name = name };
                await artist.SaveAsync();
                artists.Add(artist);
            }

            // Generate additional artists to reach 200
            for (int i = _artistNames.Length; i < 200; i++)
            {
                var artist = new Artist { Name = $"Artist {i + 1}" };
                await artist.SaveAsync();
                artists.Add(artist);
            }
        }
        return artists;
    }

    private async Task<List<Album>> SeedAlbumsAsync(List<Artist> artists, IProgress<string>? progress)
    {
        progress?.Report("Seeding 400 albums...");

        var albums = new List<Album>();
        var albumTitles = new[] { "Greatest Hits", "Live", "The Best Of", "Unplugged", "Acoustic",
            "Anthology", "Collection", "Classics", "Volume 1", "Volume 2", "Deluxe Edition" };

        // Use a single transaction for all inserts
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            for (int i = 0; i < 400; i++)
            {
                var artist = artists[_random.Next(artists.Count)];
                var titleSuffix = albumTitles[_random.Next(albumTitles.Length)];

                var album = new Album
                {
                    Title = $"{artist.Name} - {titleSuffix} {_random.Next(1980, 2025)}",
                    ArtistId = artist.id
                };
                await album.SaveAsync();
                albums.Add(album);

                if (i % 50 == 0 && i > 0)
                {
                    progress?.Report($"Seeded {i}/400 albums...");
                }
            }

            await transaction.CommitTransactionAsync();
        }

        return albums;
    }

    private async Task<List<Track>> SeedTracksAsync(List<Album> albums, List<MediaType> mediaTypes,
        List<Genre> genres, IProgress<string>? progress)
    {
        progress?.Report("Seeding 3,500 tracks...");

        var tracks = new List<Track>();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Use a single transaction for all inserts
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            // Batch size: insert every N records to reduce SaveAsync overhead
            const int batchSize = 100;
            var batch = new List<Track>();

            for (int i = 0; i < 3500; i++)
            {
                var album = albums[_random.Next(albums.Count)];
                var mediaType = mediaTypes[_random.Next(mediaTypes.Count)];
                var genre = genres[_random.Next(genres.Count)];

                var track = new Track
                {
                    Name = i < _trackTitles.Length ? _trackTitles[i] : $"Track {i + 1}",
                    AlbumId = album.id,
                    MediaTypeId = mediaType.id,
                    GenreId = genre.id,
                    Composer = _random.Next(2) == 0 ? $"{_firstNames[_random.Next(_firstNames.Length)]} {_lastNames[_random.Next(_lastNames.Length)]}" : null,
                    Milliseconds = _random.Next(120000, 420000), // 2-7 minutes
                    Bytes = _random.Next(2000000, 10000000),
                    UnitPrice = (decimal)(_random.Next(69, 199) / 100.0), // $0.69 - $1.99
                    TrackNumber = _random.Next(1, 20)
                };

                batch.Add(track);
                tracks.Add(track);

                // Insert batch when it reaches batch size or at the end
                if (batch.Count >= batchSize || i == 3499)
                {
                    // Save all tracks in batch with minimal awaits
                    foreach (var t in batch)
                    {
                        await t.SaveAsync();
                    }
                    batch.Clear();

                    if (i % 500 == 0 && i > 0)
                    {
                        var elapsed = stopwatch.Elapsed.TotalSeconds;
                        var rate = i / elapsed;
                        System.Diagnostics.Debug.WriteLine($"DIAGNOSTIC: Seeded {i}/3,500 tracks in {elapsed:F1}s ({rate:F1} tracks/sec)");
                        progress?.Report($"Seeded {i}/3,500 tracks...");
                    }
                }
            }

            await transaction.CommitTransactionAsync();
        }

        stopwatch.Stop();
        System.Diagnostics.Debug.WriteLine($"DIAGNOSTIC: Total time to seed 3,500 tracks: {stopwatch.Elapsed.TotalSeconds:F1}s ({3500 / stopwatch.Elapsed.TotalSeconds:F1} tracks/sec)");

        return tracks;
    }

    private async Task<List<Playlist>> SeedPlaylistsAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding 50 playlists...");

        var playlists = new List<Playlist>();
        var playlistNames = new[]
        {
            "My Favorites", "Workout Mix", "Chill Vibes", "Party Hits", "Road Trip",
            "Study Music", "Running Playlist", "Classic Rock", "90s Throwback", "Top 40"
        };

        for (int i = 0; i < 50; i++)
        {
            var name = i < playlistNames.Length ? playlistNames[i] : $"Playlist {i + 1}";
            var playlist = new Playlist { Name = name };
            await playlist.SaveAsync();
            playlists.Add(playlist);
        }

        return playlists;
    }

    private async Task SeedPlaylistTracksAsync(List<Playlist> playlists, List<Track> tracks,
        IProgress<string>? progress)
    {
        progress?.Report("Seeding 10,000 playlist-track relationships...");

        var addedPairs = new HashSet<string>();
        const int batchSize = 1000;
        var batch = new List<PlaylistTrack>();

        // Use a single transaction for all inserts - MASSIVE performance improvement
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            for (int i = 0; i < 10000; i++)
            {
                var playlist = playlists[_random.Next(playlists.Count)];
                var track = tracks[_random.Next(tracks.Count)];
                var key = $"{playlist.id}_{track.id}";

                if (!addedPairs.Contains(key))
                {
                    var playlistTrack = new PlaylistTrack
                    {
                        PlaylistId = playlist.id,
                        TrackId = track.id
                    };
                    batch.Add(playlistTrack);
                    addedPairs.Add(key);

                    // Insert batch when it reaches batch size
                    if (batch.Count >= batchSize)
                    {
                        foreach (var pt in batch)
                        {
                            await pt.SaveAsync();
                        }
                        batch.Clear();
                        progress?.Report($"Seeded {i + 1}/10,000 playlist tracks...");
                    }
                }
            }

            // Insert any remaining items
            if (batch.Count > 0)
            {
                foreach (var pt in batch)
                {
                    await pt.SaveAsync();
                }
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();
        }
    }

    private async Task<List<Employee>> SeedEmployeesAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding 50 employees...");

        var employees = new List<Employee>();
        var titles = new[] { "Sales Support Agent", "Sales Manager", "IT Manager", "IT Staff", "General Manager" };

        for (int i = 0; i < 50; i++)
        {
            var employee = new Employee
            {
                FirstName = _firstNames[_random.Next(_firstNames.Length)],
                LastName = _lastNames[_random.Next(_lastNames.Length)],
                Title = titles[_random.Next(titles.Length)],
                ReportsTo = i > 5 ? employees[_random.Next(Math.Min(5, employees.Count))].id : null,
                BirthDate = DateTime.Now.AddYears(-_random.Next(25, 60)),
                HireDate = DateTime.Now.AddYears(-_random.Next(1, 20)),
                Address = $"{_random.Next(100, 9999)} {_lastNames[_random.Next(_lastNames.Length)]} St",
                City = _cities[_random.Next(_cities.Length)],
                State = "CA",
                Country = _countries[_random.Next(_countries.Length)],
                PostalCode = $"{_random.Next(10000, 99999)}",
                Phone = $"+1 (555) {_random.Next(100, 999)}-{_random.Next(1000, 9999)}",
                Email = $"employee{i + 1}@chinook.com"
            };
            await employee.SaveAsync();
            employees.Add(employee);
        }

        return employees;
    }

    private async Task<List<Customer>> SeedCustomersAsync(List<Employee> employees,
        IProgress<string>? progress)
    {
        progress?.Report("Seeding 500 customers...");

        var customers = new List<Customer>();

        // Use a single transaction for all inserts
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            for (int i = 0; i < 500; i++)
            {
                var customer = new Customer
                {
                    FirstName = _firstNames[_random.Next(_firstNames.Length)],
                    LastName = _lastNames[_random.Next(_lastNames.Length)],
                    Company = _random.Next(3) == 0 ? $"{_lastNames[_random.Next(_lastNames.Length)]} Inc." : null,
                    Address = $"{_random.Next(100, 9999)} {_lastNames[_random.Next(_lastNames.Length)]} Ave",
                    City = _cities[_random.Next(_cities.Length)],
                    State = "CA",
                    Country = _countries[_random.Next(_countries.Length)],
                    PostalCode = $"{_random.Next(10000, 99999)}",
                    Phone = $"+1 (555) {_random.Next(100, 999)}-{_random.Next(1000, 9999)}",
                    Email = $"customer{i + 1}@email.com",
                    SupportRepId = employees[_random.Next(employees.Count)].id
                };
                await customer.SaveAsync();
                customers.Add(customer);

                if (i % 100 == 0 && i > 0)
                {
                    progress?.Report($"Seeded {i}/500 customers...");
                }
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();
        }

        return customers;
    }

    private async Task<List<Invoice>> SeedInvoicesAsync(List<Customer> customers,
        IProgress<string>? progress)
    {
        progress?.Report("Seeding 2,000 invoices...");

        var invoices = new List<Invoice>();

        // Use a single transaction for all inserts
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            for (int i = 0; i < 2000; i++)
            {
                var customer = customers[_random.Next(customers.Count)];
                var invoice = new Invoice
                {
                    CustomerId = customer.id,
                    InvoiceDate = DateTime.Now.AddDays(-_random.Next(1, 1095)), // Last 3 years
                    BillingAddress = customer.Address,
                    BillingCity = customer.City,
                    BillingState = customer.State,
                    BillingCountry = customer.Country,
                    BillingPostalCode = customer.PostalCode,
                    Total = 0 // Will be calculated when adding invoice lines
                };
                await invoice.SaveAsync();
                invoices.Add(invoice);

                if (i % 500 == 0 && i > 0)
                {
                    progress?.Report($"Seeded {i}/2,000 invoices...");
                }
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();
        }

        return invoices;
    }

    private async Task SeedInvoiceLinesAsync(List<Invoice> invoices, List<Track> tracks,
        IProgress<string>? progress)
    {
        progress?.Report("Seeding 8,000 invoice lines...");

        // Track invoice total updates in memory to avoid 8000 individual invoice updates
        var invoiceTotals = new Dictionary<long, decimal>();
        const int batchSize = 1000;
        var batch = new List<InvoiceLine>();

        // Use a single transaction for all inserts - MASSIVE performance improvement
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            for (int i = 0; i < 8000; i++)
            {
                var invoice = invoices[_random.Next(invoices.Count)];
                var track = tracks[_random.Next(tracks.Count)];
                var quantity = _random.Next(1, 4);

                var invoiceLine = new InvoiceLine
                {
                    InvoiceId = invoice.id,
                    TrackId = track.id,
                    UnitPrice = track.UnitPrice,
                    Quantity = quantity
                };
                batch.Add(invoiceLine);

                // Accumulate invoice totals in memory
                var lineTotal = track.UnitPrice * quantity;
                if (!invoiceTotals.ContainsKey(invoice.id))
                    invoiceTotals[invoice.id] = invoice.Total;
                invoiceTotals[invoice.id] += lineTotal;

                // Insert batch when it reaches batch size
                if (batch.Count >= batchSize)
                {
                    foreach (var il in batch)
                    {
                        await il.SaveAsync();
                    }
                    batch.Clear();
                    progress?.Report($"Seeded {i + 1}/8,000 invoice lines...");
                }
            }

            // Insert any remaining invoice lines
            if (batch.Count > 0)
            {
                foreach (var il in batch)
                {
                    await il.SaveAsync();
                }
            }

            // Now update all invoice totals in one batch
            progress?.Report("Updating invoice totals...");
            foreach (var kvp in invoiceTotals)
            {
                var invoice = invoices.First(inv => inv.id == kvp.Key);
                invoice.Total = kvp.Value;
                await invoice.SaveAsync();
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();
        }
    }
}
