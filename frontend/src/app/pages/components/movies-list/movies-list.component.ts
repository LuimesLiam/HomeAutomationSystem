import { Component, EventEmitter, Inject, Input, OnInit, Output, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { VideoService } from '../../../service/video.service';

@Component({
    selector: 'app-movies-list',
    templateUrl: './movies-list.component.html',
    styleUrls: ['./movies-list.component.scss'],
    standalone: false
})
export class MoviesListComponent implements OnInit {
  @Output() play = new EventEmitter<any>();
  @Input() movies: Record<string, any[]> = {};
  loadedGenres = new Set<string>();
  sampleSize = 10;
  showGenreOverlay = false;
  overlayGenre: string | null = null;
  overlayMovies: any[] = [];
  overlayLoading = false;
  overlaySearch = '';
  overlayPageIndex = 0;
  overlayPageSize = 20;
  overlayPageSizeOptions = [10, 20, 50];
  overlayTotalCount = 0;

  constructor(
    private videoService: VideoService,
    @Inject(PLATFORM_ID) private platformId: Object) {}

  ngOnInit(): void {
    // Only fetch movies if not provided as input
    if (isPlatformBrowser(this.platformId) && (!this.movies || Object.keys(this.movies).length === 0)) {
      this.videoService.getMoviesSample(this.sampleSize).subscribe(data => this.movies = data);
    }
  }

  onPlay(movie: any): void {
    this.play.emit(movie);
  }

  openGenreOverlay(genre: string): void {
    this.overlayGenre = genre;
    this.showGenreOverlay = true;
    this.overlaySearch = '';
    this.overlayPageIndex = 0;
    this.loadedGenres.add(genre);
    this.fetchOverlayPage();
  }

  closeGenreOverlay(): void {
    this.showGenreOverlay = false;
    this.overlayGenre = null;
    this.overlayMovies = [];
    this.overlayLoading = false;
    this.overlaySearch = '';
    this.overlayPageIndex = 0;
    this.overlayTotalCount = 0;
  }

  playFromOverlay(movie: any): void {
    this.onPlay(movie);
    this.closeGenreOverlay();
  }

  get overlayTotalPages(): number {
    return Math.max(1, Math.ceil(this.overlayTotalCount / this.overlayPageSize));
  }

  updateOverlaySearch(value: string): void {
    this.overlaySearch = value;
    this.overlayPageIndex = 0;
    this.fetchOverlayPage();
  }

  setOverlayPageIndex(index: number): void {
    const clamped = Math.min(Math.max(index, 0), this.overlayTotalPages - 1);
    this.overlayPageIndex = clamped;
    this.fetchOverlayPage();
  }

  setOverlayPageSize(value: string): void {
    const nextSize = Number(value);
    if (!Number.isFinite(nextSize) || nextSize <= 0) return;
    this.overlayPageSize = nextSize;
    this.overlayPageIndex = 0;
    this.fetchOverlayPage();
  }

  trackByMovieId(index: number, movie: any): number {
    return movie.id || index;
  }

  private fetchOverlayPage(): void {
    if (!this.overlayGenre) return;
    this.overlayLoading = true;
    this.videoService.getMoviesByGenrePaged(this.overlayGenre, this.overlayPageIndex, this.overlayPageSize, this.overlaySearch)
      .subscribe({
        next: (data) => {
          this.overlayMovies = data?.items ?? [];
          this.overlayTotalCount = data?.total ?? 0;
          this.overlayLoading = false;
        },
        error: (err) => {
          console.error('Failed to load genre movies:', err);
          this.overlayMovies = [];
          this.overlayTotalCount = 0;
          this.overlayLoading = false;
        }
      });
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'data:image/svg+xml,' + encodeURIComponent(`
      <svg xmlns="http://www.w3.org/2000/svg" width="200" height="300" viewBox="0 0 200 300">
        <rect fill="#2d2d2d" width="200" height="300"/>
        <text fill="#666" x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="14">No Image</text>
      </svg>
    `);
  }
}
