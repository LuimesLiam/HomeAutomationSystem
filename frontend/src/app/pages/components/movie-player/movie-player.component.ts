import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { interval } from 'rxjs';
import { VideoService } from '../../../service/video.service';
import { ActivatedRoute, Router } from '@angular/router';
@Component({
    selector: 'app-movie-player',
    templateUrl: './movie-player.component.html',
    styleUrls: ['./movie-player.component.scss'],
    standalone: false
})
export class MoviePlayerComponent implements OnInit, AfterViewInit {
  items: any[] = [];
  videos: Record<string, any[]> = {};
  episodes: Record<string, any[]> = {};
  defaultVideos: Record<string, any[]> = {};
  defaultEpisodes: Record<string, any[]> = {};
  loaded = false;
  searchQuery: string = '';
  isSearchActive = false;
  searchLoading = false;

  get filteredItems(): any[] {
    if (!this.searchQuery.trim()) {
      return this.items;
    }
    const query = this.searchQuery.trim().toLowerCase();
    return this.items.filter(item =>
      (item.name || '').toLowerCase().includes(query)
    );
  }

  get filteredMovies(): Record<string, any[]> {
    if (!this.searchQuery.trim() || this.isSearchActive) {
      return this.videos;
    }
    const query = this.searchQuery.trim().toLowerCase();
    const filtered: Record<string, any[]> = {};
    Object.keys(this.videos).forEach(genre => {
      const matchingMovies = this.videos[genre].filter((movie: any) => 
        (movie.title || '').toLowerCase().includes(query) ||
        (movie.name || '').toLowerCase().includes(query) ||
        (movie.genre || '').toLowerCase().includes(query)
      );
      if (matchingMovies.length > 0) {
        filtered[genre] = matchingMovies;
      }
    });
    return filtered;
  }

  get filteredEpisodes(): Record<string, any[]> {
    if (!this.searchQuery.trim() || this.isSearchActive) {
      return this.episodes;
    }
    const query = this.searchQuery.trim().toLowerCase();
    const filtered: Record<string, any[]> = {};
    Object.keys(this.episodes).forEach(genre => {
      const matchingSeries = this.episodes[genre].filter((series: any) => 
        (series.title || '').toLowerCase().includes(query) ||
        (series.name || '').toLowerCase().includes(query)
      );
      if (matchingSeries.length > 0) {
        filtered[genre] = matchingSeries;
      }
    });
    return filtered;
  }

  videoPath!:string; 
  playing= false;
  playbackMode: string = 'website';
  loading = false;
  syncing = false;
  selectedMedia: any | null = null;
  addressMode: 'browser' | 'home' = 'browser';
  playbackAddressLoading = false;
  playbackAddressError: string | null = null;
  isEditingDetails = false;
  editSaving = false;
  editSyncing = false;
  editError: string | null = null;
  editModel = {
    title: '',
    year: '',
    genre: '',
    description: ''
  };

  timeBarValue = 0;
  currentTimeDisplay = '0:00';
  totalTimeDisplay = '0:00';
  totalDuration = 0;
  isDragging = false;

  seekValue: number = 0;
  currentTime: number = 0;
  duration: number = 0;
  selectedTabIndex: number = 0;
  readonly previewLimit = 10;


  @ViewChild('videoPlayer') videoPlayer!: ElementRef<HTMLVideoElement>;

  displayedMovieColumns: string[] = ['thumbnail', 'title', 'year', 'genre'];
  displayedEpisodeColumns: string[] = ['thumbnail', 'series', 'season', 'title'];

  constructor(
    private videoService: VideoService,
    private route: ActivatedRoute,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    const mode = this.route.snapshot.queryParamMap.get('mode');
    const url = this.route.snapshot.queryParamMap.get('url');
    const filePath = this.route.snapshot.queryParamMap.get('filePath');
    const hasPlaybackParams = !!mode && (!!url || !!filePath);

    if (!hasPlaybackParams && isPlatformBrowser(this.platformId)) {
      this.loadMoviesAndEpisodes();
    }
    
    if (isPlatformBrowser(this.platformId)) {
      interval(1000).subscribe(() => {
        if (!this.isDragging && this.playing == true &&this.playbackMode =='tv' && 5 <( this.totalDuration - this.timeBarValue )) {
          this.syncTimeBar();
        }
      });
    }

    this.route.queryParams.subscribe(params => {
      const mode = params['mode'];
      const url = params['url'];
      const filePath = params['filePath'];

      if (!mode) return;

      if (mode === 'website' && url) {
        this.playbackMode = 'website';
        this.videoPath = this.getBrowserPlaybackUrl(url);
        this.playing = true;
        setTimeout(() => {
          if (this.videoPlayer) {
            this.videoPlayer.nativeElement.play();
          }
        });
      } else if (mode === 'url' && url) {
        this.playbackMode = 'url';
        this.videoPath = url;
        this.playing = true;
      } else if (mode === 'tv' && filePath) {
        this.playbackMode = 'tv';
        this.videoPath = filePath;
        this.playing = true;
        this.videoService.playServerVideo(filePath);
      }
    });
  }

  private moviesLoaded = false;
  private episodesLoaded = false;

  private hasEpisodeSeasons(): boolean {
    return Object.values(this.episodes).some(seriesList =>
      Array.isArray(seriesList) && seriesList.some(series => {
        const seasons = series?.seasons;
        return seasons && Object.keys(seasons).length > 0;
      })
    );
  }

  private loadMoviesAndEpisodes(): void {
    this.loading = true;
    this.moviesLoaded = false;
    this.episodesLoaded = false;
    
    this.videoService.getMoviesSample(this.previewLimit).subscribe({
      next: (moviesData) => {
        this.videos = moviesData || {};
        this.defaultVideos = moviesData || {};
        this.moviesLoaded = true;
        this.checkLoadingComplete();
      },
      error: (err) => {
        console.error('Failed to load movies:', err);
        this.moviesLoaded = true;
        this.checkLoadingComplete();
      }
    });

    this.videoService.getSeriesSample(this.previewLimit).subscribe({
      next: (episodesData) => {
        this.episodes = episodesData || {};
        this.defaultEpisodes = episodesData || {};
        this.episodesLoaded = true;
        this.checkLoadingComplete();
      },
      error: (err) => {
        console.error('Failed to load episodes:', err);
        this.episodesLoaded = true;
        this.checkLoadingComplete();
      }
    });
  }

  private checkLoadingComplete(): void {
    if (this.moviesLoaded && this.episodesLoaded) {
      this.loading = false;
      this.loaded = true;
    }
  }

  syncMovies(): void {
    this.syncing = true;
    this.videoService.syncMovies().subscribe({
      next: () => {
        // Reload movies after sync
        this.videoService.getMoviesSample(this.previewLimit).subscribe({
          next: (moviesData) => {
            this.videos = moviesData;
            this.defaultVideos = moviesData || {};
            this.syncing = false;
          },
          error: (err) => {
            console.error('Failed to reload movies after sync:', err);
            this.syncing = false;
          }
        });
      },
      error: (err) => {
        console.error('Failed to sync movies:', err);
        this.syncing = false;
      }
    });
  }

  syncTv(): void {
    this.syncing = true;
    this.videoService.syncTv().subscribe({
      next: () => {
        // Reload episodes after sync
        this.videoService.getSeriesSample(this.previewLimit).subscribe({
          next: (episodesData) => {
            this.episodes = episodesData || {};
            this.defaultEpisodes = episodesData || {};
            this.syncing = false;
          },
          error: (err) => {
            console.error('Failed to reload episodes after sync:', err);
            this.syncing = false;
          }
        });
      },
      error: (err) => {
        console.error('Failed to sync episodes:', err);
        this.syncing = false;
      }
    });
  }

  ngAfterViewInit(): void {
    // if(this.playbackMode =='website'){}
    // this.videoPlayer.nativeElement.addEventListener('loadedmetadata', () => {
    //   this.timeBarValue = this.videoPlayer.nativeElement.duration;
    // });
  }

  seekVideo(value: number) {
    const video: HTMLVideoElement = this.videoPlayer.nativeElement;
    console.log(video.currentTime, value,this.timeBarValue, video);
    video.currentTime = 20.042218 ;
  }

  closeVideo() {
    this.videoPath = '';
    this.playing = false;
    if (!this.hasEpisodeSeasons()) {
      this.loadMoviesAndEpisodes();
    }
    // if(this.playbackMode == 'tv'){
    //   this.controlVideo('stop');
    // }
  }

  copyToClipboard(url: string): void {
    // Encode the URL
    url = this.getEncodedUrl(url);
  
    // Check if navigator.clipboard is supported
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(url).then(
        () => alert('URL copied to clipboard!'),
        (err) => {
          console.error('Failed to copy URL using clipboard API: ', err);
          this.fallbackCopyToClipboard(url);
        }
      );
    } else {
      // Fallback for unsupported browsers
      this.fallbackCopyToClipboard(url);
    }
  }
  
  private fallbackCopyToClipboard(url: string): void {
    // Create a temporary input element to hold the URL
    const tempInput = document.createElement('input');
    tempInput.value = url;
    document.body.appendChild(tempInput);
  
    // Select the text in the input
    tempInput.select();
    tempInput.setSelectionRange(0, url.length); // For mobile compatibility
  
    // Execute the copy command
    try {
      const successful = document.execCommand('copy');
      if (successful) {
        alert('URL copied to clipboard!');
      } else {
        console.error('Fallback copy command failed.');
      }
    } catch (err) {
      console.error('Error during fallback copy: ', err);
    }
  
    // Remove the temporary input element
    document.body.removeChild(tempInput);
  }
  


  updateSeekTime(value: number): void {
    const seekTime = (value / 100) * this.totalDuration;
    this.currentTimeDisplay = this.formatTime(seekTime);
  }

  seekToPosition(value: number): void {
    this.isDragging = false;
    const seekTime = (value / 100) * this.totalDuration;
    this.videoService.sendCommand('set_time', undefined, Math.round(seekTime)).subscribe();
  }

  syncTimeBar(): void {
    this.videoService.sendCommand('get_time').subscribe((data: any) => {
      this.applyTvPlaybackStatus(data);
    });
  }

  formatTime(ms: number): string {
    const totalSeconds = Math.floor(ms / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }


  search(): void {
    const query = this.searchQuery.trim();

    if (!query) {
      this.clearSearch();
      return;
    }

    this.searchLoading = true;
    this.isSearchActive = true;

    if (this.selectedTabIndex === 1) {
      this.videoService.searchSeries(query).subscribe({
        next: (episodesData) => {
          this.episodes = episodesData || {};
          this.searchLoading = false;
        },
        error: (err) => {
          console.error('Failed to search TV series:', err);
          this.episodes = {};
          this.searchLoading = false;
        }
      });
      return;
    }

    this.videoService.searchMovies(query).subscribe({
      next: (moviesData) => {
        this.videos = moviesData || {};
        this.searchLoading = false;
      },
      error: (err) => {
        console.error('Failed to search movies:', err);
        this.videos = {};
        this.searchLoading = false;
      }
    });
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.isSearchActive = false;
    this.searchLoading = false;
    this.videos = this.defaultVideos;
    this.episodes = this.defaultEpisodes;
  }
  

  /**
   * Plays a video file.
   * @param videoName - Name of the video to play
   */
  playMovie(movie: any): void {
    this.playing = true;
    this.videoPath = this.playbackMode == 'website'
      ? this.getBrowserPlaybackUrl(movie.videoUrl)
      : this.playbackMode == 'url'
        ? movie.videoUrl
        : movie.filePath;

    // Mark as watched
    if (movie.id) {
      movie.watched = true;
      if (movie.season !== undefined || movie.episodeNumber !== undefined) {
        // It's an episode
        this.videoService.setEpisodeWatched(movie.id, true).subscribe({
          error: (err) => console.error('Failed to mark episode as watched', err)
        });
      } else {
        // It's a movie
        this.videoService.setMovieWatched(movie.id, true).subscribe({
          error: (err) => console.error('Failed to mark movie as watched', err)
        });
      }
    }

    if (this.playbackMode == 'tv') {
      this.videoService.playServerVideo(`${movie.filePath}`);
    } else if (this.playbackMode == 'website') {
      setTimeout(() => {
        if (this.videoPlayer) {
          this.videoPlayer.nativeElement.play();
        }
      });
    }
  }

  playEpisode(ep: any): void {
    this.playMovie(ep);
  }

  openMediaDetails(media: any): void {
    this.selectedMedia = media;
    this.addressMode = 'browser';
    this.playbackAddressLoading = false;
    this.playbackAddressError = null;
    this.isEditingDetails = false;
    this.editError = null;
  }

  closeMediaDetails(): void {
    this.selectedMedia = null;
    this.playbackAddressLoading = false;
    this.playbackAddressError = null;
    this.isEditingDetails = false;
    this.editError = null;
  }

  isMovieSelection(): boolean {
    if (!this.selectedMedia?.id) return false;
    return this.selectedMedia.season === undefined && this.selectedMedia.episodeNumber === undefined;
  }

  startEditDetails(): void {
    if (!this.selectedMedia) return;
    this.editModel = {
      title: this.selectedMedia.title || '',
      year: this.selectedMedia.year || '',
      genre: this.selectedMedia.genre || '',
      description: this.selectedMedia.description || this.selectedMedia.plot || ''
    };
    this.editError = null;
    this.isEditingDetails = true;
  }

  cancelEditDetails(): void {
    this.isEditingDetails = false;
    this.editError = null;
  }

  saveMovieDetails(): void {
    if (!this.selectedMedia?.id) return;
    this.editSaving = true;
    this.editError = null;

    const payload = {
      title: this.editModel.title,
      year: this.editModel.year,
      genre: this.editModel.genre,
      description: this.editModel.description
    };

    this.videoService.updateMovie(this.selectedMedia.id, payload).subscribe({
      next: (updated) => {
        this.selectedMedia = { ...this.selectedMedia, ...updated };
        this.editSaving = false;
        this.isEditingDetails = false;
        this.refreshMoviesList();
      },
      error: (err) => {
        console.error('Failed to update movie:', err);
        this.editError = 'Failed to update movie. Please try again.';
        this.editSaving = false;
      }
    });
  }

  resyncMovieDetails(): void {
    if (!this.selectedMedia?.id) return;
    this.editSyncing = true;
    this.editError = null;

    this.videoService.resyncMovie(this.selectedMedia.id).subscribe({
      next: (updated) => {
        this.selectedMedia = { ...this.selectedMedia, ...updated };
        this.editSyncing = false;
        this.refreshMoviesList();
      },
      error: (err) => {
        console.error('Failed to resync movie:', err);
        this.editError = 'Failed to resync movie from OMDB.';
        this.editSyncing = false;
      }
    });
  }

  private refreshMoviesList(): void {
    this.videoService.getMoviesSample(this.previewLimit).subscribe({
      next: (moviesData) => {
        this.videos = moviesData || {};
        this.defaultVideos = moviesData || {};
      },
      error: (err) => {
        console.error('Failed to refresh movies list:', err);
      }
    });
  }

  onTabChange(nextTabIndex: number): void {
    this.selectedTabIndex = nextTabIndex;

    if (!this.searchQuery.trim()) {
      return;
    }

    this.search();
  }

  startPlayback(mode: string, media: any): void {
    if (mode === 'tv') {
      this.playbackAddressLoading = false;
      this.playbackAddressError = null;
      this.playbackMode = mode;
      this.selectedMedia = null;
      this.playMovie(media);
      return;
    }

    if (!media?.filePath) {
      this.playbackAddressError = 'This item is missing a file path.';
      return;
    }

    this.playbackAddressLoading = true;
    this.playbackAddressError = null;

    this.videoService.getPlaybackOptions(media.filePath).subscribe({
      next: (options) => {
        if (this.addressMode === 'home' && !options.homeUrl) {
          this.playbackAddressLoading = false;
          this.playbackAddressError = 'Home playback is not configured yet.';
          return;
        }

        const selectedUrl = this.addressMode === 'home' && options.homeUrl
          ? options.homeUrl
          : options.browserUrl;

        const playbackMedia = {
          ...media,
          videoUrl: selectedUrl
        };

        this.playbackAddressLoading = false;
        this.playbackMode = mode;
        this.selectedMedia = null;
        this.playMovie(playbackMedia);
      },
      error: (err) => {
        console.error('Failed to build playback URLs:', err);
        this.playbackAddressLoading = false;
        this.playbackAddressError = 'Could not build the selected playback address.';
      }
    });
  }

  canUseHomeAddress(): boolean {
    return !!this.selectedMedia?.filePath && !this.playbackAddressLoading;
  }

  onDetailImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img) {
      img.src = 'assets/placeholder-poster.svg';
    }
  }

  private getBrowserPlaybackUrl(url: string): string {
    if (!url) {
      return url;
    }

    try {
      const parsedUrl = new URL(url, window.location.origin);
      if (parsedUrl.pathname.startsWith('/media/')) {
        parsedUrl.searchParams.set('browser', 'true');
      }
      return parsedUrl.toString();
    } catch (error) {
      console.error('Invalid media URL format:', error);
      return url;
    }
  }

  getEncodedUrl(url: string): string {
    try {
      // Parse the URL
      const parsedUrl = new URL(url);
  
      // Decode the pathname first to prevent double encoding
      const decodedPath = decodeURIComponent(parsedUrl.pathname);
  
      // Encode special characters in each segment of the pathname
      const encodedPath = decodedPath
        .split('/') // Split the path into segments
        .map(segment => encodeURIComponent(segment)) // Encode each segment
        .join('/'); // Rejoin the segments with '/'
  
      // Recombine origin, encoded path, and search query
      return `${parsedUrl.origin}${encodedPath}${parsedUrl.search}`;
    } catch (error) {
      console.error('Invalid URL format:', error);
      return url; // Return the original URL if parsing fails
    }
  }
  
  
  

  controlVideo(command: string): void {
    if (this.playbackMode === 'tv') {
      this.videoService.sendCommand(command, command === 'play' ? this.videoPath : undefined)
        .subscribe({
          next: (response) => {
            this.playing = response?.playing ?? (command === 'resume' || command === 'play');
            if (command === 'stop') {
              this.playing = false;
              this.timeBarValue = 0;
              this.currentTimeDisplay = '0:00';
            }
            this.applyTvPlaybackStatus(response);
          },
          error: (error) => console.error(error),
        });
    }
  }

  seekTvBy(seconds: number): void {
    if (this.playbackMode !== 'tv') {
      return;
    }

    this.videoService.sendCommand('seek', undefined, seconds).subscribe({
      next: (response) => this.applyTvPlaybackStatus(response),
      error: (error) => console.error(error)
    });
  }

  private applyTvPlaybackStatus(data: any): void {
    if (!data) {
      return;
    }

    this.playing = typeof data.playing === 'boolean' ? data.playing : this.playing;
    this.totalDuration = data.total_time ?? this.totalDuration;
    const currentTime = data.current_time ?? 0;
    this.timeBarValue = this.totalDuration > 0 ? (currentTime / this.totalDuration) * 100 : 0;
    this.currentTimeDisplay = this.formatTime(currentTime);
    this.totalTimeDisplay = this.formatTime(this.totalDuration);
  }

  // Helper to extract season/episode or fallback to name for sorting
  private static extractSortKey(name: string): { season: number, episode: number, name: string } {
    // Match s1e2, S01E02, s01e02, etc.
    const match = name.match(/s(\d+)[\s\-_\.]*e(\d+)/i);
    if (match) {
      return {
        season: parseInt(match[1], 10),
        episode: parseInt(match[2], 10),
        name: name.toLowerCase()
      };
    }
    // fallback: try to extract just a number (e.g. "Episode 12")
    const numMatch = name.match(/(\d+)/);
    return {
      season: 0,
      episode: numMatch ? parseInt(numMatch[1], 10) : 0,
      name: name.toLowerCase()
    };
  }

  private static videoSort(a: any, b: any): number {
    // Only sort videos, folders stay at the top (or sort folders alphabetically)
    if (a.type === 'folder' && b.type === 'folder') {
      return a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' });
    }
    if (a.type === 'folder') return -1;
    if (b.type === 'folder') return 1;

    // Both are videos
    const keyA = MoviePlayerComponent.extractSortKey(a.name || '');
    const keyB = MoviePlayerComponent.extractSortKey(b.name || '');
    if (keyA.season !== keyB.season) return keyA.season - keyB.season;
    if (keyA.episode !== keyB.episode) return keyA.episode - keyB.episode;
    return keyA.name.localeCompare(keyB.name, undefined, { numeric: true, sensitivity: 'base' });
  }

}
