import { Component, Inject, OnInit, PLATFORM_ID, TrackByFunction, computed, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { VideoService } from '../../../service/video.service';

interface Movie {
  id?: number;
  title: string;
  year?: string;
  genre?: string;
  description?: string;
  plot?: string;
  thumbnail?: string;
  filePath: string;
  videoUrl?: string;
  hidden?: boolean;
  watched?: boolean;
}

@Component({
  selector: 'app-movies-table',
  templateUrl: './movies-table.component.html',
  styleUrls: ['./movies-table.component.scss'],
  standalone: false
})
export class MoviesTableComponent implements OnInit {
  readonly displayedColumns: string[] = ['thumbnail', 'title', 'year', 'genre', 'watched', 'actions'];
  readonly placeholderImage = 'assets/placeholder-poster.svg';
  readonly pageSizeOptions = [10, 20, 50, 100];

  readonly movies = signal<Movie[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);

  readonly selectedRow = signal<Movie | null>(null);
  readonly showOverlay = signal(false);
  readonly isEditing = signal(false);
  readonly editSaving = signal(false);
  readonly editSyncing = signal(false);
  readonly editError = signal<string | null>(null);

  editModel = {
    title: '',
    year: '',
    genre: '',
    description: ''
  };

  readonly filteredMovies = computed(() => {
    const query = this.searchTerm().trim().toLowerCase();
    if (!query) {
      return this.movies();
    }

    return this.movies().filter((movie) => {
      const haystack = [movie.title, movie.year, movie.genre, movie.description, movie.plot]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return haystack.includes(query);
    });
  });

  readonly pagedMovies = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filteredMovies().slice(start, start + this.pageSize());
  });

  readonly totalRecords = computed(() => this.filteredMovies().length);

  constructor(
    private videoService: VideoService,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      this.isLoading.set(false);
      return;
    }

    this.loadMovies();
  }

  trackByMovie: TrackByFunction<Movie> = (index, movie) => movie.id ?? movie.filePath ?? index;

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

  openOverlay(row: Movie): void {
    this.selectedRow.set(row);
    this.showOverlay.set(true);
    this.isEditing.set(false);
    this.editError.set(null);
  }

  closeOverlay(): void {
    this.showOverlay.set(false);
    this.selectedRow.set(null);
    this.isEditing.set(false);
    this.editError.set(null);
  }

  startEdit(): void {
    const selected = this.selectedRow();
    if (!selected) {
      return;
    }

    this.editModel = {
      title: selected.title || '',
      year: selected.year || '',
      genre: selected.genre || '',
      description: selected.description || selected.plot || ''
    };

    this.editError.set(null);
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
    this.editError.set(null);
  }

  saveEdit(): void {
    const selected = this.selectedRow();
    if (!selected?.id) {
      return;
    }

    this.editSaving.set(true);
    this.editError.set(null);

    this.videoService
      .updateMovie(selected.id, {
        title: this.editModel.title,
        year: this.editModel.year,
        genre: this.editModel.genre,
        description: this.editModel.description
      })
      .subscribe({
        next: (updated) => {
          this.selectedRow.set({ ...selected, ...updated });
          this.editSaving.set(false);
          this.isEditing.set(false);
          this.loadMovies();
        },
        error: (err) => {
          console.error('Failed to update movie:', err);
          this.editError.set('Failed to update movie. Please try again.');
          this.editSaving.set(false);
        }
      });
  }

  resyncMovie(): void {
    const selected = this.selectedRow();
    if (!selected?.id) {
      return;
    }

    this.editSyncing.set(true);
    this.editError.set(null);

    this.videoService.resyncMovie(selected.id).subscribe({
      next: (updated) => {
        this.selectedRow.set({ ...selected, ...updated });
        this.editSyncing.set(false);
        this.loadMovies();
      },
      error: (err) => {
        console.error('Failed to resync movie:', err);
        this.editError.set('Failed to resync movie from OMDB.');
        this.editSyncing.set(false);
      }
    });
  }

  playOnTv(row: Movie): void {
    if (row?.filePath) {
      this.router.navigate(['movie-player'], {
        queryParams: { mode: 'tv', filePath: row.filePath }
      });
    }
    this.closeOverlay();
  }

  openOnWebsite(row: Movie): void {
    if (row?.videoUrl) {
      this.router.navigate(['movie-player'], {
        queryParams: { mode: 'website', url: row.videoUrl }
      });
    }
    this.closeOverlay();
  }

  displayUrl(row: Movie): void {
    if (row?.videoUrl) {
      this.router.navigate(['movie-player'], {
        queryParams: { mode: 'url', url: row.videoUrl }
      });
    }
    this.closeOverlay();
  }

  retry(): void {
    this.loadMovies();
  }

  private loadMovies(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.videoService.getAllMoviesList().subscribe({
      next: (data) => {
        this.movies.set(data || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load movies:', err);
        this.error.set('Failed to load movies. Please try again.');
        this.isLoading.set(false);
      }
    });
  }
}
