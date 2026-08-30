import { Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { VideoService } from '../../../service/video.service';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

interface Episode {
  id?: number;
  episodeNumber: number;
  season: number;
  title: string;
  description?: string;
  thumbnail?: string;
  filePath: string;
  videoUrl?: string;
  watched?: boolean;
}

interface Series {
  id?: number;
  title: string;
  description?: string;
  thumbnail?: string;
  genre?: string;
  episodes?: Episode[];
  seasons?: Record<string, Episode[]>;
}

@Component({
    selector: 'app-tv-list',
    templateUrl: './tv-list.component.html',
    styleUrls: ['./tv-list.component.scss'],
    standalone: false
})
export class TvListComponent implements OnInit, OnDestroy {
  @Input() shows: Record<string, Series[]> = {};
  @Output() play = new EventEmitter<Episode>();
  
  selectedSeries: Series | null = null;
  loadedGenres = new Set<string>();
  sampleSize = 10;
  isLoading = false;
  isSeriesLoading = false;
  
  private destroy$ = new Subject<void>();

  constructor(
    private videoService: VideoService,
    @Inject(PLATFORM_ID) private platformId: Object) {}

  ngOnInit(): void {
    // Only fetch series if not provided as input
    if (isPlatformBrowser(this.platformId) && (!this.shows || Object.keys(this.shows).length === 0)) {
      this.loadInitialData();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadInitialData(): void {
    this.isLoading = true;
    this.videoService.getSeriesSample(this.sampleSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.shows = this.transformData(data);
          this.isLoading = false;
        },
        error: (err) => {
          console.error('Failed to load TV shows:', err);
          this.isLoading = false;
        }
      });
  }

  onSeriesSelected(series: Series): void {
    if (series.seasons && Object.keys(series.seasons).length > 0) {
      this.selectedSeries = series;
      return;
    }

    if (!series.id) {
      this.selectedSeries = series;
      return;
    }

    this.selectedSeries = series;
    this.isSeriesLoading = true;
    this.videoService.getSeriesEpisodes(series.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (episodes) => {
          const seasons = this.buildSeasons(episodes || []);
          series.episodes = episodes;
          series.seasons = seasons;
          this.selectedSeries = series;
          this.isSeriesLoading = false;
        },
        error: (err) => {
          console.error('Failed to load episodes:', err);
          this.isSeriesLoading = false;
        }
      });
  }

  onCloseDetail(): void {
    this.selectedSeries = null;
    this.isSeriesLoading = false;
  }

  onPlay(ep: Episode): void {
    this.play.emit(ep);
  }

  loadGenre(genre: string): void {
    if (this.loadedGenres.has(genre)) return;
    
    this.videoService.getSeriesByGenre(genre)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          const transformed = this.transformData({ [genre]: data });
          this.shows[genre] = transformed[genre];
          this.loadedGenres.add(genre);
        },
        error: (err) => {
          console.error(`Failed to load genre ${genre}:`, err);
        }
      });
  }

  private transformData(data: Record<string, Series[]>): Record<string, Series[]> {
    const transformed: Record<string, Series[]> = {};
    
    Object.keys(data).forEach(genre => {
      const genreData = data[genre];
      const seriesArr: Series[] = Array.isArray(genreData) ? genreData : [];

      seriesArr.forEach((series: Series) => {
        let episodes: Episode[] = series.episodes || [];

        episodes = episodes.map((ep: Episode) => ({
          ...ep,
          thumbnail: ep.thumbnail || series.thumbnail,
          description: ep.description,
          title: ep.title,
          episodeNumber: ep.episodeNumber,
          season: ep.season,
          filePath: ep.filePath,
          videoUrl: ep.videoUrl
        }));

        const dedupedEpisodes = this.dedupeEpisodes(episodes);

        // Group episodes by season
        const seasons: Record<string, Episode[]> = {};
        dedupedEpisodes.forEach((ep: Episode) => {
          const seasonNum = ep.season !== undefined && ep.season !== null ? String(ep.season) : null;
          if (seasonNum === null || seasonNum === 'undefined') return;
          
          if (!seasons[seasonNum]) {
            seasons[seasonNum] = [];
          }
          seasons[seasonNum].push(ep);
        });
        
        // Sort episodes within each season
        Object.values(seasons).forEach(seasonEps => {
          seasonEps.sort((a, b) => a.episodeNumber - b.episodeNumber);
        });
        
        series.episodes = dedupedEpisodes;
        series.seasons = seasons;
      });
      
      transformed[genre] = seriesArr;
    });
    
    return transformed;
  }

  private buildSeasons(episodes: Episode[]): Record<string, Episode[]> {
    const dedupedEpisodes = this.dedupeEpisodes(episodes);
    const seasons: Record<string, Episode[]> = {};
    dedupedEpisodes.forEach((ep: Episode) => {
      const seasonNum = ep.season !== undefined && ep.season !== null ? String(ep.season) : null;
      if (seasonNum === null || seasonNum === 'undefined') return;
      if (!seasons[seasonNum]) {
        seasons[seasonNum] = [];
      }
      seasons[seasonNum].push({
        ...ep,
        thumbnail: ep.thumbnail
      });
    });
    Object.values(seasons).forEach(seasonEps => {
      seasonEps.sort((a, b) => a.episodeNumber - b.episodeNumber);
    });
    return seasons;
  }

  private dedupeEpisodes(episodes: Episode[]): Episode[] {
    const byKey = new Map<string, Episode>();
    let fallbackIndex = 0;
    for (const ep of episodes) {
      const hasNumbers = ep.season !== undefined && ep.episodeNumber !== undefined;
      const key = hasNumbers
        ? `${ep.season}-${ep.episodeNumber}`
        : `unknown-${ep.filePath || ep.id || fallbackIndex++}`;
      const existing = byKey.get(key);
      if (!existing) {
        byKey.set(key, { ...ep });
        continue;
      }

      const merged: Episode = { ...existing };
      if (!merged.id && ep.id) merged.id = ep.id;
      if (!merged.title && ep.title) merged.title = ep.title;
      if (!merged.description && ep.description) merged.description = ep.description;
      if (!merged.thumbnail && ep.thumbnail) merged.thumbnail = ep.thumbnail;
      if (!merged.filePath && ep.filePath) merged.filePath = ep.filePath;
      if (!merged.videoUrl && ep.videoUrl) merged.videoUrl = ep.videoUrl;
      merged.watched = !!(merged.watched || ep.watched);
      byKey.set(key, merged);
    }
    return Array.from(byKey.values());
  }
}
