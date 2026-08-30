using HomeApp.Videos.Configuration;
using HomeApp.Library.Database.EntityFramework;
using HomeApp.Videos.Logic;
using HomeApp.Videos.Services;
using HomeApp.Videos.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeApp.Videos;

public static class HostExtensions
{
    public static IServiceCollection AddHomeAppVideos(
        this IServiceCollection services,
        IConfiguration config,
        VideoModuleOptions? options = null)
    {
        var videoOptions = options ?? VideoModuleOptions.FromConfiguration(config);
        services.AddSingleton(videoOptions);
        services.AddHomeAppPostgresDbContext<HomeAppVideosDbContext>(videoOptions.PostgresConnection);
        services.AddControllers()
            .AddApplicationPart(typeof(HostExtensions).Assembly);
        services.AddMemoryCache();
        services.AddScoped<VideoManager>();
        services.AddScoped<MediaSourceService>();

        services.AddScoped<MovieRepository>();
        services.AddScoped<TvRepository>();

        services.AddHttpClient<IOmdbService, OmdbService>();
        services.AddHttpClient<IImageService, ImageService>();

        services.AddScoped<IMovieRepository, CachedMovieRepository>();
        services.AddScoped<ISeriesRepository, CachedSeriesRepository>();

        services.AddScoped<IMediaSyncService, MediaSyncService>();

        return services;
    }
}
