using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HomeApp.Videos.Logic;

public class TvRepository
{
    private readonly HomeAppVideosDbContext _db;

    public TvRepository(HomeAppVideosDbContext db)
    {
        _db = db;
    }

    public List<Episode> GetAllEpisodes()
    {
        return _db.Episodes.Include(e => e.Series).ToList();
    }

    public List<Series> GetAllSeries()
    {
        return _db.Series
            .Include(s => s.Episodes)
            .Where(s => !s.Hidden)
            .ToList();
    }

    public HashSet<string> ExistingEpisodePaths()
    {
        return _db.Episodes.Select(e => e.FilePath).ToHashSet();
    }

    public Series GetOrCreateSeries(string name)
    {
        var series = _db.Series.FirstOrDefault(s => s.Name == name);
        if (series == null)
        {
            series = new Series { Name = name, Title = name, Hidden = false };
            _db.Series.Add(series);
            _db.SaveChanges();
        }
        return series;
    }

    public void AddEpisodes(IEnumerable<Episode> episodes)
    {
        _db.Episodes.AddRange(episodes);
        _db.SaveChanges();
    }

    public Dictionary<string, List<Series>> GetSeriesByGenre()
    {
        var seriesList = _db.Series
            .Where(s => !s.Hidden)
            .ToList();
        return GroupSeriesByGenre(seriesList);
    }

    public List<Series> GetSeriesByGenre(string genre)
    {
        return _db.Series
            .Where(s => !s.Hidden)
            .AsEnumerable()
            .Where(s => (s.Genre ?? "Unknown")
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .Contains(genre))
            .ToList();
    }

    public List<Series> SearchSeries(string query, int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<Series>();
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();

        return _db.Series
            .Where(s => !s.Hidden)
            .AsEnumerable()
            .Where(s =>
                (s.Title ?? s.Name ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery) ||
                (s.Genre ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery) ||
                (s.Description ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery))
            .OrderBy(s => s.Title ?? s.Name)
            .Take(maxResults)
            .ToList();
    }

    public List<Episode> GetEpisodesForSeries(int seriesId)
    {
        return _db.Episodes
            .Where(e => !e.Hidden && e.SeriesId == seriesId)
            .OrderBy(e => e.Season)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();
    }

    public List<Episode> GetEpisodesForSeriesSeason(int seriesId, int season)
    {
        return _db.Episodes
            .Where(e => e.SeriesId == seriesId && e.Season == season)
            .OrderBy(e => e.EpisodeNumber)
            .ToList();
    }

    public Series? GetSeriesById(int id)
    {
        return _db.Series.FirstOrDefault(s => s.Id == id && !s.Hidden);
    }

    public bool UpdateSeries(Series series)
    {
        _db.Series.Update(series);
        return _db.SaveChanges() > 0;
    }

    public bool UpdateEpisodes(IEnumerable<Episode> episodes)
    {
        _db.Episodes.UpdateRange(episodes);
        return _db.SaveChanges() > 0;
    }

    public async System.Threading.Tasks.Task<bool> SetWatchedAsync(int id, bool watched)
    {
        var episode = await _db.Episodes.FindAsync(id);
        if (episode == null) return false;
        
        episode.Watched = watched;
        await _db.SaveChangesAsync();
        return true;
    }

    private static Dictionary<string, List<Series>> GroupSeriesByGenre(List<Series> seriesList)
    {
        var result = new Dictionary<string, List<Series>>();
        foreach (var s in seriesList)
        {
            var genres = (s.Genre ?? "Unknown").Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            if (genres.Length == 0) genres = new[] { "Unknown" };
            foreach (var g in genres)
            {
                if (!result.ContainsKey(g))
                    result[g] = new List<Series>();
                result[g].Add(s);
            }
        }
        return result;
    }
}
