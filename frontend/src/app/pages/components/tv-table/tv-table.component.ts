import { Component, TrackByFunction, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TvEpisode, TvSeriesListItem, VideoService } from '../../../service/video.service';

interface SeriesDetailItem extends TvSeriesListItem {
  episodes?: TvEpisode[];
  seasons?: Record<string, TvEpisode[]>;
}

@Component({
  selector: 'app-tv-table',
  templateUrl: './tv-table.component.html',
  styleUrls: ['./tv-table.component.scss'],
  standalone: false
})
export class TvTableComponent {
  readonly displayedColumns: string[] = ['thumbnail', 'series', 'genre', 'seasons', 'episodes', 'watched', 'actions'];
  readonly placeholderImage = 'assets/placeholder-poster.svg';
  readonly pageSizeOptions = [10, 20, 50, 100];

  readonly series = signal<SeriesDetailItem[]>([]);
  readonly isLoading = signal(true);
  readonly isSeriesLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedSeries = signal<SeriesDetailItem | null>(null);
  readonly searchTerm = signal('');
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);

  readonly filteredSeries = computed(() => {
    const query = this.searchTerm().trim().toLowerCase();
    if (!query) {
      return this.series();
    }

    return this.series().filter((item) => {
      const haystack = [
        item.title,
        item.name,
        item.genre,
        item.description,
        String(item.seasonCount),
        String(item.episodeCount),
        String(item.watchedEpisodeCount)
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      return haystack.includes(query);
    });
  });

  readonly pagedSeries = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filteredSeries().slice(start, start + this.pageSize());
  });

  readonly totalRecords = computed(() => this.filteredSeries().length);

  constructor(
    private videoService: VideoService,
    private router: Router
  ) {
    this.loadSeries();
  }

  trackBySeries: TrackByFunction<SeriesDetailItem> = (index, item) => item.id ?? item.title ?? index;

  applyFilter(value: string): void {
    this.searchTerm.set(value);
    this.pageIndex.set(0);
  }

  onPageChange(event: { first?: number; rows?: number }): void {
    const rows = event.rows ?? this.pageSize();
    const first = event.first ?? 0;
    this.pageSize.set(rows);
    this.pageIndex.set(Math.floor(first / rows));
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img && img.src !== this.placeholderImage) {
      img.src = this.placeholderImage;
    }
  }

  openSeries(seriesItem: SeriesDetailItem): void {
    if (!seriesItem.id || (seriesItem.seasons && Object.keys(seriesItem.seasons).length > 0)) {
      this.selectedSeries.set(seriesItem);
      return;
    }

    this.selectedSeries.set(seriesItem);
    this.isSeriesLoading.set(true);

    this.videoService.getSeriesEpisodes(seriesItem.id).subscribe({
      next: (episodes) => {
        seriesItem.episodes = episodes;
        seriesItem.seasons = this.buildSeasons(episodes || []);
        this.selectedSeries.set(seriesItem);
        this.isSeriesLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load series episodes:', err);
        this.isSeriesLoading.set(false);
      }
    });
  }

  closeSeriesDetail(): void {
    this.selectedSeries.set(null);
    this.isSeriesLoading.set(false);
  }

  playEpisode(episode: TvEpisode): void {
    if (episode.videoUrl) {
      this.router.navigate(['movie-player'], {
        queryParams: { mode: 'website', url: episode.videoUrl }
      });
      return;
    }

    if (episode.filePath) {
      this.router.navigate(['movie-player'], {
        queryParams: { mode: 'tv', filePath: episode.filePath }
      });
    }
  }

  formatWatched(seriesItem: SeriesDetailItem): string {
    return `${seriesItem.watchedEpisodeCount}/${seriesItem.episodeCount}`;
  }

  retry(): void {
    this.loadSeries();
  }

  private loadSeries(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.videoService.getAllSeriesList().subscribe({
      next: (data) => {
        this.series.set(data || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load series:', err);
        this.error.set('Failed to load TV series. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  private buildSeasons(episodes: TvEpisode[]): Record<string, TvEpisode[]> {
    const seasons: Record<string, TvEpisode[]> = {};

    episodes.forEach((episode) => {
      const seasonKey = episode.season !== undefined && episode.season !== null ? String(episode.season) : null;
      if (!seasonKey || seasonKey === 'undefined') {
        return;
      }

      if (!seasons[seasonKey]) {
        seasons[seasonKey] = [];
      }

      seasons[seasonKey].push(episode);
    });

    Object.values(seasons).forEach((seasonEpisodes) => {
      seasonEpisodes.sort((left, right) => left.episodeNumber - right.episodeNumber);
    });

    return seasons;
  }
}
