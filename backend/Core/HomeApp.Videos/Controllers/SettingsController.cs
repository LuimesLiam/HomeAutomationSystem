using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomeApp.Videos.Controllers;

[ApiController]
[Route("api/settings/media-sources")]
public sealed class SettingsController : ControllerBase
{
    private readonly MediaSourceService _mediaSourceService;

    public SettingsController(MediaSourceService mediaSourceService)
    {
        _mediaSourceService = mediaSourceService;
    }

    [HttpGet]
    public async Task<ActionResult<MediaSourcesResponseDto>> GetMediaSources(CancellationToken ct)
    {
        var sources = await _mediaSourceService.GetAllAsync(ct);
        return Ok(MapResponse(sources));
    }

    [HttpPut]
    public async Task<ActionResult<MediaSourcesResponseDto>> UpdateMediaSources(
        [FromBody] MediaSourcesSaveRequestDto request,
        CancellationToken ct)
    {
        var saved = await _mediaSourceService.SaveAsync(
            request.MovieSources.Select(source => new MediaSource
            {
                Id = source.Id,
                SourceType = MediaSourceService.MovieType,
                Path = source.Path
            }).Concat(request.TvSources.Select(source => new MediaSource
            {
                Id = source.Id,
                SourceType = MediaSourceService.TvType,
                Path = source.Path
            })),
            ct);

        return Ok(MapResponse(saved));
    }

    private static MediaSourcesResponseDto MapResponse(IEnumerable<MediaSource> sources)
    {
        return new MediaSourcesResponseDto
        {
            MovieSources = sources
                .Where(source => source.SourceType == MediaSourceService.MovieType)
                .OrderBy(source => source.DisplayOrder)
                .ThenBy(source => source.Id)
                .Select(MapItem)
                .ToList(),
            TvSources = sources
                .Where(source => source.SourceType == MediaSourceService.TvType)
                .OrderBy(source => source.DisplayOrder)
                .ThenBy(source => source.Id)
                .Select(MapItem)
                .ToList()
        };
    }

    private static MediaSourceItemDto MapItem(MediaSource source)
    {
        return new MediaSourceItemDto
        {
            Id = source.Id,
            Path = source.Path
        };
    }
}

public sealed class MediaSourcesResponseDto
{
    public List<MediaSourceItemDto> MovieSources { get; set; } = [];
    public List<MediaSourceItemDto> TvSources { get; set; } = [];
}

public sealed class MediaSourcesSaveRequestDto
{
    public List<MediaSourceItemDto> MovieSources { get; set; } = [];
    public List<MediaSourceItemDto> TvSources { get; set; } = [];
}

public sealed class MediaSourceItemDto
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
}
