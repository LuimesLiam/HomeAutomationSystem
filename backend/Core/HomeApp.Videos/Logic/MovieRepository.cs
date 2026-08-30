using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using System.Collections.Generic;
using System.Linq;

namespace HomeApp.Videos.Logic;

public class MovieRepository
{
    private readonly HomeAppVideosDbContext _db;

    public MovieRepository(HomeAppVideosDbContext db)
    {
        _db = db;
    }

    public List<Movie> GetAll()
    {
        return _db.Movies.ToList();
    }

    public Movie? GetById(int id)
    {
        return _db.Movies.FirstOrDefault(m => m.Id == id);
    }

    public Dictionary<string, List<Movie>> GetByGenre()
    {
        var result = new Dictionary<string, List<Movie>>();
        var movies = _db.Movies.Where(m => !m.Hidden).ToList();
        foreach (var movie in movies)
        {
            var genres = (movie.Genre ?? "Unknown").Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            if (genres.Length == 0) genres = new[] { "Unknown" };
            foreach (var g in genres)
            {
                if (!result.ContainsKey(g))
                    result[g] = new List<Movie>();
                result[g].Add(movie);
            }
        }
        return result;
    }

    public List<Movie> GetByGenre(string genre)
    {
        return _db.Movies
            .Where(m => !m.Hidden)
            .AsEnumerable()
            .Where(m => (m.Genre ?? "Unknown")
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .Contains(genre))
            .ToList();
    }

    public (List<Movie> Items, int Total) GetByGenrePaged(string genre, int page, int pageSize, string? search = null)
    {
        var filtered = _db.Movies
            .Where(m => !m.Hidden)
            .AsEnumerable()
            .Where(m => (m.Genre ?? "Unknown")
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .Contains(genre));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var query = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(m =>
                (m.Title ?? m.Name ?? string.Empty).ToLowerInvariant().Contains(query) ||
                (m.Year ?? string.Empty).ToLowerInvariant().Contains(query));
        }

        var total = filtered.Count();
        var items = filtered
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, total);
    }

    public List<Movie> Search(string query, int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<Movie>();
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();

        return _db.Movies
            .Where(m => !m.Hidden)
            .AsEnumerable()
            .Where(m =>
                (m.Title ?? m.Name ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery) ||
                (m.Year ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery) ||
                (m.Genre ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery))
            .OrderBy(m => m.Title ?? m.Name)
            .Take(maxResults)
            .ToList();
    }

    public bool Exists(string filePath)
    {
        return _db.Movies.Any(m => m.FilePath == filePath);
    }

    public void Add(Movie movie)
    {
        _db.Movies.Add(movie);
        _db.SaveChanges();
    }

    public void AddRange(IEnumerable<Movie> movies)
    {
        _db.Movies.AddRange(movies);
        _db.SaveChanges();
    }

    public void Update(Movie movie)
    {
        _db.Movies.Update(movie);
        _db.SaveChanges();
    }

    public void SetWatched(int id, bool watched)
    {
        var movie = _db.Movies.Find(id);
        if (movie != null)
        {
            movie.Watched = watched;
            _db.SaveChanges();
        }
    }
}
