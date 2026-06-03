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
        try
        {
            progress?.Report("Starting database seeding...");

            // Register all entities with SQLiteXM
            await RegisterEntitiesAsync();

            // Check if we have partial data (interrupted seeding)
            // If tables exist but are empty or incomplete, we'll detect it in individual seed methods

            // Seed reference data first
            var genres = await SeedGenresAsync(progress);
            var mediaTypes = await SeedMediaTypesAsync(progress);
            var artists = await SeedArtistsAsync(progress);

            // Seed albums and tracks (large datasets)
            var albums = await SeedAlbumsAsync(artists, progress);
            var tracks = await SeedTracksAsync(albums, mediaTypes, genres, progress);

            // Seed playlists and many-to-many relationships
            var playlists = await SeedPlaylistsAsync(progress);
            await SeedPlaylistTracksAsync(playlists, tracks, progress);

            // Seed customers and employees
            var employees = await SeedEmployeesAsync(progress);
            var customers = await SeedCustomersAsync(employees, progress);

            // Seed invoices and invoice lines
            var invoices = await SeedInvoicesAsync(customers, progress);
            await SeedInvoiceLinesAsync(invoices, tracks, progress);

            // Mark as seeded
            Preferences.Set(SEED_KEY, true);

            progress?.Report("Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            progress?.Report($"Error seeding database: {ex.Message}");
            throw;
        }
    }

    private async Task<List<Genre>> SeedGenresAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding genres...");

        // Check if genres already exist
        var existingGenres = await new SxmLinqDbContext("Chinook").GetTable<Genre>().ToListAsync();
        if (existingGenres.Count > 0)
        {
            progress?.Report($"Genres already exist ({existingGenres.Count}), skipping...");
            return existingGenres;
        }

        var genres = new List<Genre>();
        foreach (var name in _genreNames)
        {
            var genre = new Genre { Name = name };
            await genre.SaveAsync();
            genres.Add(genre);
        }

        return genres;
    }

    private async Task<List<MediaType>> SeedMediaTypesAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding media types...");

        // Check if media types already exist
        var existingMediaTypes = await new SxmLinqDbContext("Chinook").GetTable<MediaType>().ToListAsync();
        if (existingMediaTypes.Count > 0)
        {
            progress?.Report($"Media types already exist ({existingMediaTypes.Count}), skipping...");
            return existingMediaTypes;
        }

        var mediaTypes = new List<MediaType>();
        foreach (var name in _mediaTypeNames)
        {
            var mediaType = new MediaType { Name = name };
            await mediaType.SaveAsync();
            mediaTypes.Add(mediaType);
        }

        return mediaTypes;
    }

    private async Task<List<Artist>> SeedArtistsAsync(IProgress<string>? progress)
    {
        progress?.Report("Seeding 200 artists...");

        // Check if artists already exist
        var existingArtists = await new SxmLinqDbContext("Chinook").GetTable<Artist>().ToListAsync();
        if (existingArtists.Count > 0)
        {
            progress?.Report($"Artists already exist ({existingArtists.Count}), skipping...");
            return existingArtists;
        }

        var artists = new List<Artist>();

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

        return artists;
    }

    private async Task<List<Album>> SeedAlbumsAsync(List<Artist> artists, IProgress<string>? progress)
    {
        progress?.Report("Seeding 400 albums...");

        var albums = new List<Album>();
        var albumTitles = new[] { "Greatest Hits", "Live", "The Best Of", "Unplugged", "Acoustic",
            "Anthology", "Collection", "Classics", "Volume 1", "Volume 2", "Deluxe Edition" };

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

        return albums;
    }

    private async Task<List<Track>> SeedTracksAsync(List<Album> albums, List<MediaType> mediaTypes,
        List<Genre> genres, IProgress<string>? progress)
    {
        progress?.Report("Seeding 3,500 tracks...");

        var tracks = new List<Track>();

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
            await track.SaveAsync();
            tracks.Add(track);

            if (i % 500 == 0 && i > 0)
            {
                progress?.Report($"Seeded {i}/3,500 tracks...");
            }
        }

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
                await playlistTrack.SaveAsync();
                addedPairs.Add(key);
            }

            if (i % 1000 == 0 && i > 0)
            {
                progress?.Report($"Seeded {i}/10,000 playlist tracks...");
            }
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

        return customers;
    }

    private async Task<List<Invoice>> SeedInvoicesAsync(List<Customer> customers,
        IProgress<string>? progress)
    {
        progress?.Report("Seeding 2,000 invoices...");

        var invoices = new List<Invoice>();

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

        return invoices;
    }

    private async Task SeedInvoiceLinesAsync(List<Invoice> invoices, List<Track> tracks,
        IProgress<string>? progress)
    {
        progress?.Report("Seeding 8,000 invoice lines...");

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
            await invoiceLine.SaveAsync();

            // Update invoice total
            invoice.Total += track.UnitPrice * quantity;
            await invoice.SaveAsync();

            if (i % 1000 == 0 && i > 0)
            {
                progress?.Report($"Seeded {i}/8,000 invoice lines...");
            }
        }
    }
}
