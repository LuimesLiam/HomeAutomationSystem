import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import {
  LlmSettingsItem,
  MediaSyncCandidate,
  MediaSourceItem,
  VideoService
} from '../../../service/video.service';

@Component({
  selector: 'app-settings',
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, ProgressSpinnerModule]
})
export class SettingsComponent implements OnInit {
  readonly settingsSections = ['ai', 'video'] as const;
  readonly activeSection = signal<'ai' | 'video'>('ai');
  readonly movieSyncLimit = signal(100);
  readonly tvSyncLimit = signal(100);
  readonly movieSources = signal<MediaSourceItem[]>([this.createSource()]);
  readonly tvSources = signal<MediaSourceItem[]>([this.createSource()]);
  readonly syncingMovies = signal(false);
  readonly syncingTv = signal(false);
  readonly loadingSettings = signal(false);
  readonly savingSettings = signal(false);
  readonly loadingAiSettings = signal(false);
  readonly savingAiSettings = signal(false);
  readonly movieSyncMessage = signal('');
  readonly tvSyncMessage = signal('');
  readonly movieCandidates = signal<MediaSyncCandidate[]>([]);
  readonly tvCandidates = signal<MediaSyncCandidate[]>([]);
  readonly selectedMoviePaths = signal<Set<string>>(new Set());
  readonly selectedTvPaths = signal<Set<string>>(new Set());
  readonly movieCandidatesLoaded = signal(false);
  readonly tvCandidatesLoaded = signal(false);
  readonly loadingMovieCandidates = signal(false);
  readonly loadingTvCandidates = signal(false);
  readonly movieCandidateSearch = signal('');
  readonly tvCandidateSearch = signal('');
  readonly movieCandidateSort = signal('title-asc');
  readonly tvCandidateSort = signal('series-asc');
  readonly settingsMessage = signal('');
  readonly aiSettingsMessage = signal('');
  readonly llms = signal<LlmSettingsItem[]>([]);

  constructor(private videoService: VideoService) {}

  ngOnInit(): void {
    this.loadMediaSourceSettings();
    this.loadAiSettings();
  }

  loadMediaSourceSettings(): void {
    this.loadingSettings.set(true);
    this.settingsMessage.set('');

    this.videoService.getMediaSourceSettings().subscribe({
      next: (settings) => {
        this.movieSources.set(this.normalizeSources(settings.movieSources));
        this.tvSources.set(this.normalizeSources(settings.tvSources));
        this.loadingSettings.set(false);
      },
      error: () => {
        this.settingsMessage.set('Failed to load media source settings.');
        this.loadingSettings.set(false);
      }
    });
  }

  updateMovieSourcePath(index: number, value: string): void {
    const next = [...this.movieSources()];
    next[index] = { ...next[index], path: value };
    this.movieSources.set(next);
  }

  updateTvSourcePath(index: number, value: string): void {
    const next = [...this.tvSources()];
    next[index] = { ...next[index], path: value };
    this.tvSources.set(next);
  }

  addMovieSource(): void {
    this.movieSources.set([...this.movieSources(), this.createSource()]);
  }

  removeMovieSource(index: number): void {
    this.movieSources.set(this.removeSourceAt(this.movieSources(), index));
  }

  addTvSource(): void {
    this.tvSources.set([...this.tvSources(), this.createSource()]);
  }

  removeTvSource(index: number): void {
    this.tvSources.set(this.removeSourceAt(this.tvSources(), index));
  }

  saveMediaSourceSettings(): void {
    const movieSources = this.compactSources(this.movieSources());
    const tvSources = this.compactSources(this.tvSources());

    this.savingSettings.set(true);
    this.settingsMessage.set('');

    this.videoService.updateMediaSourceSettings({ movieSources, tvSources }).subscribe({
      next: (settings) => {
        this.movieSources.set(this.normalizeSources(settings.movieSources));
        this.tvSources.set(this.normalizeSources(settings.tvSources));
        this.settingsMessage.set('Media source folders saved.');
        this.savingSettings.set(false);
      },
      error: () => {
        this.settingsMessage.set('Failed to save media source folders.');
        this.savingSettings.set(false);
      }
    });
  }

  loadAiSettings(): void {
    this.loadingAiSettings.set(true);
    this.aiSettingsMessage.set('');

    this.videoService.getAiSettings().subscribe({
      next: (settings) => {
        this.llms.set(this.normalizeLlms(settings.llms ?? []));
        this.loadingAiSettings.set(false);
      },
      error: () => {
        this.aiSettingsMessage.set('Failed to load AI settings.');
        this.loadingAiSettings.set(false);
      }
    });
  }

  addLlm(): void {
    this.llms.set([...this.llms(), this.createLlm()]);
  }

  removeLlm(index: number): void {
    const next = this.llms().filter((_, currentIndex) => currentIndex !== index);
    if (next.length > 0 && !next.some((llm) => llm.isDefault)) {
      next[0] = { ...next[0], isDefault: true };
    }
    this.llms.set(next);
  }

  updateLlmField<K extends keyof LlmSettingsItem>(index: number, field: K, value: LlmSettingsItem[K]): void {
    if (field === 'isDefault' && value) {
      this.llms.set(this.llms().map((llm, currentIndex) => ({ ...llm, isDefault: currentIndex === index })));
      return;
    }
    this.llms.set(this.updateItem(this.llms(), index, (llm) => ({ ...llm, [field]: value })));
  }

  saveAiSettings(): void {
    const llms = this.compactLlms(this.llms());
    if (llms.length === 0) {
      this.aiSettingsMessage.set('Add at least one LLM before saving.');
      return;
    }

    this.savingAiSettings.set(true);
    this.aiSettingsMessage.set('');

    this.videoService.updateAiSettings({ llms }).subscribe({
      next: (settings) => {
        this.llms.set(this.normalizeLlms(settings.llms ?? []));
        this.aiSettingsMessage.set('LLM settings saved.');
        this.savingAiSettings.set(false);
      },
      error: (error) => {
        this.aiSettingsMessage.set(error.error?.message ?? 'Failed to save AI settings.');
        this.savingAiSettings.set(false);
      }
    });
  }

  syncMovies(): void {
    const limit = this.normalizeLimit(this.movieSyncLimit());
    if (!limit) {
      this.movieSyncMessage.set('Enter a positive number to sync movies.');
      return;
    }

    this.syncingMovies.set(true);
    this.movieSyncMessage.set('');

    const selectedPaths = this.movieCandidatesLoaded()
      ? Array.from(this.selectedMoviePaths()).slice(0, limit)
      : undefined;

    this.videoService.syncMovies(limit, selectedPaths).subscribe({
      next: (result) => {
        const added = result?.added ?? 0;
        this.movieSyncMessage.set(`Added ${added} selected movie${added === 1 ? '' : 's'}.`);
        this.syncingMovies.set(false);
        if (this.movieCandidatesLoaded()) {
          this.loadMovieCandidates(true);
        }
      },
      error: () => {
        this.movieSyncMessage.set('Failed to sync movies.');
        this.syncingMovies.set(false);
      }
    });
  }

  syncTv(): void {
    const limit = this.normalizeLimit(this.tvSyncLimit());
    if (!limit) {
      this.tvSyncMessage.set('Enter a positive number to sync episodes.');
      return;
    }

    this.syncingTv.set(true);
    this.tvSyncMessage.set('');

    const selectedPaths = this.tvCandidatesLoaded()
      ? Array.from(this.selectedTvPaths()).slice(0, limit)
      : undefined;

    this.videoService.syncTv(limit, selectedPaths).subscribe({
      next: (result) => {
        const added = result?.added ?? 0;
        this.tvSyncMessage.set(`Added ${added} selected episode${added === 1 ? '' : 's'}.`);
        this.syncingTv.set(false);
        if (this.tvCandidatesLoaded()) {
          this.loadTvCandidates(true);
        }
      },
      error: () => {
        this.tvSyncMessage.set('Failed to sync episodes.');
        this.syncingTv.set(false);
      }
    });
  }

  loadMovieCandidates(preserveMessage = false): void {
    this.loadingMovieCandidates.set(true);
    if (!preserveMessage) {
      this.movieSyncMessage.set('');
    }

    this.videoService.getMovieSyncCandidates().subscribe({
      next: (items) => {
        this.movieCandidates.set(items);
        this.selectedMoviePaths.set(new Set(items.map((item) => item.path)));
        this.movieCandidatesLoaded.set(true);
        this.loadingMovieCandidates.set(false);
      },
      error: () => {
        this.movieSyncMessage.set('Failed to scan for new movies.');
        this.loadingMovieCandidates.set(false);
      }
    });
  }

  loadTvCandidates(preserveMessage = false): void {
    this.loadingTvCandidates.set(true);
    if (!preserveMessage) {
      this.tvSyncMessage.set('');
    }

    this.videoService.getTvSyncCandidates().subscribe({
      next: (items) => {
        this.tvCandidates.set(items);
        this.selectedTvPaths.set(new Set(items.map((item) => item.path)));
        this.tvCandidatesLoaded.set(true);
        this.loadingTvCandidates.set(false);
      },
      error: () => {
        this.tvSyncMessage.set('Failed to scan for new TV episodes.');
        this.loadingTvCandidates.set(false);
      }
    });
  }

  filteredMovieCandidates(): MediaSyncCandidate[] {
    return this.filterAndSortCandidates(
      this.movieCandidates(),
      this.movieCandidateSearch(),
      this.movieCandidateSort()
    );
  }

  filteredTvCandidates(): MediaSyncCandidate[] {
    return this.filterAndSortCandidates(
      this.tvCandidates(),
      this.tvCandidateSearch(),
      this.tvCandidateSort()
    );
  }

  setCandidateSelected(kind: 'movie' | 'tv', path: string, selected: boolean): void {
    const current = new Set(kind === 'movie' ? this.selectedMoviePaths() : this.selectedTvPaths());
    selected ? current.add(path) : current.delete(path);
    kind === 'movie' ? this.selectedMoviePaths.set(current) : this.selectedTvPaths.set(current);
  }

  selectAllCandidates(kind: 'movie' | 'tv'): void {
    const items = kind === 'movie' ? this.movieCandidates() : this.tvCandidates();
    const selected = new Set(items.map((item) => item.path));
    kind === 'movie' ? this.selectedMoviePaths.set(selected) : this.selectedTvPaths.set(selected);
  }

  deselectAllCandidates(kind: 'movie' | 'tv'): void {
    kind === 'movie'
      ? this.selectedMoviePaths.set(new Set())
      : this.selectedTvPaths.set(new Set());
  }

  isCandidateSelected(kind: 'movie' | 'tv', path: string): boolean {
    return (kind === 'movie' ? this.selectedMoviePaths() : this.selectedTvPaths()).has(path);
  }

  trackByCandidatePath(_: number, item: MediaSyncCandidate): string {
    return item.path;
  }

  setActiveSection(section: 'ai' | 'video'): void {
    this.activeSection.set(section);
  }

  isActiveSection(section: 'ai' | 'video'): boolean {
    return this.activeSection() === section;
  }

  trackByIndex(index: number): number {
    return index;
  }

  private normalizeLimit(value: number): number | null {
    if (!Number.isFinite(value)) {
      return null;
    }

    const limit = Math.floor(value);
    return limit > 0 ? limit : null;
  }

  private filterAndSortCandidates(
    items: MediaSyncCandidate[],
    search: string,
    sort: string
  ): MediaSyncCandidate[] {
    const query = search.trim().toLowerCase();
    const filtered = query
      ? items.filter((item) =>
          [item.title, item.name, item.year, item.series, item.path]
            .some((value) => String(value ?? '').toLowerCase().includes(query)))
      : [...items];

    return filtered.sort((left, right) => {
      const direction = sort.endsWith('-desc') ? -1 : 1;
      if (sort.startsWith('year')) {
        return direction * String(left.year ?? '').localeCompare(String(right.year ?? ''), undefined, { numeric: true });
      }
      if (sort.startsWith('path')) {
        return direction * left.path.localeCompare(right.path, undefined, { numeric: true, sensitivity: 'base' });
      }
      if (sort.startsWith('series')) {
        const seriesComparison = String(left.series ?? '').localeCompare(
          String(right.series ?? ''),
          undefined,
          { numeric: true, sensitivity: 'base' }
        );
        return direction * (seriesComparison ||
          ((left.season ?? 0) - (right.season ?? 0)) ||
          ((left.episode ?? 0) - (right.episode ?? 0)));
      }

      return direction * String(left.title ?? left.name).localeCompare(
        String(right.title ?? right.name),
        undefined,
        { numeric: true, sensitivity: 'base' }
      );
    });
  }

  private normalizeSources(sources: MediaSourceItem[] | null | undefined): MediaSourceItem[] {
    const compact = this.compactSources(sources ?? []);
    return compact.length > 0 ? compact : [this.createSource()];
  }

  private compactSources(sources: MediaSourceItem[]): MediaSourceItem[] {
    const seen = new Set<string>();
    return sources
      .map((source) => ({ id: source.id ?? 0, path: source.path.trim() }))
      .filter((source) => {
        if (!source.path) {
          return false;
        }

        const key = source.path.toLowerCase();
        if (seen.has(key)) {
          return false;
        }

        seen.add(key);
        return true;
      });
  }

  private removeSourceAt(sources: MediaSourceItem[], index: number): MediaSourceItem[] {
    const next = sources.filter((_, currentIndex) => currentIndex !== index);
    return next.length > 0 ? next : [this.createSource()];
  }

  private createSource(): MediaSourceItem {
    return { id: 0, path: '' };
  }

  private normalizeLlms(llms: LlmSettingsItem[]): LlmSettingsItem[] {
    const normalized = llms.map((llm) => ({
      ...llm,
      paramsJson: llm.paramsJson ?? ''
    }));

    if (normalized.length > 0 && !normalized.some((llm) => llm.isDefault)) {
      normalized[0] = { ...normalized[0], isDefault: true };
    }

    return normalized.length > 0 ? normalized : [this.createLlm()];
  }

  private compactLlms(llms: LlmSettingsItem[]): LlmSettingsItem[] {
    let defaultAssigned = false;
    const compact = llms
      .map((llm) => ({
        ...llm,
        key: llm.key.trim(),
        name: llm.name.trim(),
        modelName: llm.modelName.trim(),
        provider: llm.provider.trim(),
        baseUrl: llm.baseUrl.trim(),
        apiKeyName: llm.apiKeyName.trim(),
        paramsJson: llm.paramsJson?.trim() ?? ''
      }))
      .map((llm) => {
        const isDefault = llm.isDefault && !defaultAssigned;
        defaultAssigned = defaultAssigned || isDefault;
        return { ...llm, isDefault };
      });

    if (compact.length > 0 && !compact.some((llm) => llm.isDefault)) {
      compact[0] = { ...compact[0], isDefault: true };
    }
    return compact;
  }

  private createLlm(): LlmSettingsItem {
    return {
      id: 0,
      key: '',
      name: '',
      modelName: '',
      provider: '',
      baseUrl: '',
      apiKeyName: '',
      paramsJson: '{"temperature":0.2,"maxOutputTokens":1200}',
      isEnabled: true,
      isDefault: this.llms().length === 0
    };
  }

  private updateItem<T>(items: T[], index: number, updater: (item: T) => T): T[] {
    return items.map((item, currentIndex) => currentIndex === index ? updater(item) : item);
  }

}
