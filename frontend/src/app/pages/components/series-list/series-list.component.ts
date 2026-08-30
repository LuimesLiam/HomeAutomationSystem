import { Component, EventEmitter, Input, Output, TrackByFunction } from '@angular/core';

interface Series {
  id?: number;
  title: string;
  thumbnail?: string;
  description?: string;
  seasons?: Record<string, any[]>;
}

@Component({
    selector: 'app-series-list',
    templateUrl: './series-list.component.html',
    styleUrls: ['./series-list.component.scss'],
    standalone: false
})
export class SeriesListComponent {
  @Input() shows: Record<string, Series[]> = {};
  @Input() loadedGenres: Set<string> = new Set<string>();
  @Output() seriesSelected = new EventEmitter<Series>();
  @Output() loadGenre = new EventEmitter<string>();

  /** Placeholder image for failed loads */
  readonly placeholderImage = 'assets/placeholder-poster.svg';

  /** Track series by ID for better rendering performance */
  trackBySeries: TrackByFunction<Series> = (index, series) => 
    series.id ?? series.title ?? index;

  /** Track genres by key */
  trackByGenre: TrackByFunction<{ key: string; value: Series[] }> = (index, item) => 
    item.key;

  onSeriesClick(series: Series): void {
    this.seriesSelected.emit(series);
  }

  onLoadGenre(genre: string): void {
    this.loadGenre.emit(genre);
  }

  /** Handle thumbnail load errors */
  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img && img.src !== this.placeholderImage) {
      img.src = this.placeholderImage;
    }
  }

  /** Check if a genre has series to display */
  hasContent(genre: { key: string; value: Series[] }): boolean {
    return genre.value && genre.value.length > 0;
  }

  /** Check if all episodes in a series are watched */
  isSeriesWatched(series: Series): boolean {
    if (!series.seasons) return false;
    for (const seasonKey of Object.keys(series.seasons)) {
      const episodes = series.seasons[seasonKey];
      if (Array.isArray(episodes)) {
        for (const episode of episodes) {
          if (!episode.watched) return false;
        }
      }
    }
    return Object.keys(series.seasons).length > 0;
  }
}
