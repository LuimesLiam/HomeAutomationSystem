import { KeyValue } from '@angular/common';
import { Component, EventEmitter, Input, Output, TrackByFunction, HostListener } from '@angular/core';
import { VideoService } from '../../../service/video.service';

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

interface Season {
  [seasonNumber: string]: Episode[];
}

interface Series {
  id?: number;
  title: string;
  thumbnail?: string;
  description?: string;
  genre?: string;
  episodes?: Episode[];
  seasons?: Season;
}

@Component({
    selector: 'app-series-detail',
    templateUrl: './series-detail.component.html',
    styleUrls: ['./series-detail.component.scss'],
    standalone: false
})
export class SeriesDetailComponent {
  @Input() series!: Series;
  @Input() loading = false;
  @Output() close = new EventEmitter<void>();
  @Output() play = new EventEmitter<Episode>();

  isEditing = false;
  editSaving = false;
  editSyncing = false;
  editError: string | null = null;
  editModel = {
    title: '',
    genre: '',
    description: ''
  };
  seasonSyncing: Record<string, boolean> = {};

  /** Track opened seasons for accordions */
  openedSeasons: Record<string, boolean> = {};

  /** Placeholder for failed images */
  readonly placeholderImage = 'assets/placeholder-poster.svg';

  constructor(private videoService: VideoService) {}

  /** Get sorted season numbers */
  getSeasonNumbers(): string[] {
    if (!this.series?.seasons) return [];
    return Object.keys(this.series.seasons).sort((a, b) => Number(a) - Number(b));
  }

  /** Toggle season accordion */
  toggleSeason(seasonNum: string): void {
    this.openedSeasons[seasonNum] = !this.openedSeasons[seasonNum];
  }

  /** Check if season is expanded */
  isSeasonOpen(seasonNum: string): boolean {
    return !!this.openedSeasons[seasonNum];
  }

  /** Expand all seasons */
  expandAll(): void {
    this.getSeasonNumbers().forEach(num => {
      this.openedSeasons[num] = true;
    });
  }

  /** Collapse all seasons */
  collapseAll(): void {
    this.openedSeasons = {};
  }

  /** Get episode count for a season */
  getEpisodeCount(seasonNum: string): number {
    return this.series?.seasons?.[seasonNum]?.length ?? 0;
  }

  /** Get episodes for a season */
  getSeasonEpisodes(seasonNum: string): Episode[] {
    return this.series?.seasons?.[seasonNum] ?? [];
  }

  /** Play an episode */
  onPlay(episode: Episode): void {
    this.play.emit(episode);
  }

  /** Close the detail view */
  onClose(): void {
    this.close.emit();
  }

  startEdit(): void {
    this.editModel = {
      title: this.series?.title || '',
      genre: this.series?.genre || '',
      description: this.series?.description || ''
    };
    this.editError = null;
    this.isEditing = true;
  }

  cancelEdit(): void {
    this.isEditing = false;
    this.editError = null;
  }

  saveSeries(): void {
    if (!this.series?.id) return;
    this.editSaving = true;
    this.editError = null;

    const payload = {
      title: this.editModel.title,
      genre: this.editModel.genre,
      description: this.editModel.description
    };

    this.videoService.updateSeries(this.series.id, payload).subscribe({
      next: (updated) => {
        this.series.title = updated?.title ?? this.series.title;
        this.series.genre = updated?.genre ?? this.series.genre;
        this.series.description = updated?.description ?? this.series.description;
        this.series.thumbnail = updated?.thumbnail ?? this.series.thumbnail;
        this.editSaving = false;
        this.isEditing = false;
      },
      error: (err) => {
        console.error('Failed to update series:', err);
        this.editError = 'Failed to update series. Please try again.';
        this.editSaving = false;
      }
    });
  }

  resyncSeries(): void {
    if (!this.series?.id) return;
    this.editSyncing = true;
    this.editError = null;

    this.videoService.resyncSeries(this.series.id).subscribe({
      next: (updated) => {
        this.series.title = updated?.title ?? this.series.title;
        this.series.genre = updated?.genre ?? this.series.genre;
        this.series.description = updated?.description ?? this.series.description;
        this.series.thumbnail = updated?.thumbnail ?? this.series.thumbnail;
        this.editSyncing = false;
      },
      error: (err) => {
        console.error('Failed to resync series:', err);
        this.editError = 'Failed to resync series from OMDB.';
        this.editSyncing = false;
      }
    });
  }

  resyncSeason(seasonNum: string, event?: MouseEvent): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }

    if (!this.series?.id) return;
    if (this.seasonSyncing[seasonNum]) return;

    const seasonNumber = Number(seasonNum);
    if (!Number.isFinite(seasonNumber) || seasonNumber <= 0) return;

    this.seasonSyncing[seasonNum] = true;

    this.videoService.resyncSeason(this.series.id, seasonNumber).subscribe({
      next: () => {
        this.videoService.getSeriesEpisodes(this.series!.id!).subscribe({
          next: (episodes) => {
            this.series.episodes = episodes;
            this.series.seasons = this.buildSeasons(episodes || []);
            this.seasonSyncing[seasonNum] = false;
          },
          error: (err) => {
            console.error('Failed to reload episodes after season resync:', err);
            this.seasonSyncing[seasonNum] = false;
          }
        });
      },
      error: (err) => {
        console.error('Failed to resync season:', err);
        this.seasonSyncing[seasonNum] = false;
      }
    });
  }

  isSeasonSyncing(seasonNum: string): boolean {
    return !!this.seasonSyncing[seasonNum];
  }

  private buildSeasons(episodes: Episode[]): Record<string, Episode[]> {
    const seasons: Record<string, Episode[]> = {};
    episodes.forEach((ep: Episode) => {
      const seasonKey = ep.season !== undefined && ep.season !== null ? String(ep.season) : null;
      if (!seasonKey || seasonKey === 'undefined') return;
      if (!seasons[seasonKey]) {
        seasons[seasonKey] = [];
      }
      seasons[seasonKey].push({
        ...ep,
        thumbnail: ep.thumbnail
      });
    });
    Object.values(seasons).forEach(seasonEps => {
      seasonEps.sort((a, b) => a.episodeNumber - b.episodeNumber);
    });
    return seasons;
  }

  /** Handle image load errors */
  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img && img.src !== this.placeholderImage) {
      img.src = this.placeholderImage;
    }
  }

  /** Close on Escape key */
  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    this.onClose();
  }

  /** Track functions for ngFor optimization */
  trackBySeason: TrackByFunction<string> = (index, season) => season;
  
  trackByEpisode: TrackByFunction<Episode> = (index, episode) => 
    episode.id ?? `${episode.season}-${episode.episodeNumber}`;
}
