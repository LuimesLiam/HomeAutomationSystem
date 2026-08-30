import { Component, OnInit, signal } from '@angular/core';
import {
  AiModelSettingsItem,
  AiProviderSettingsItem,
  AiProviderTypeOption,
  MediaSyncCandidate,
  MediaSourceItem,
  VideoService
} from '../../../service/video.service';

@Component({
  selector: 'app-settings',
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss'],
  standalone: false
})
export class SettingsComponent implements OnInit {
  readonly settingsSections = ['ai', 'agent', 'video'] as const;
  readonly activeSection = signal<'ai' | 'agent' | 'video'>('ai');
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
  readonly aiProviderTypes = signal<AiProviderTypeOption[]>([]);
  readonly aiProviders = signal<AiProviderSettingsItem[]>([]);
  readonly aiModels = signal<AiModelSettingsItem[]>([]);

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
        this.aiProviderTypes.set(settings.providerTypes ?? []);
        this.aiProviders.set(this.normalizeAiProviders(settings.providers ?? []));
        this.aiModels.set(this.normalizeAiModels(settings.models ?? []));
        this.loadingAiSettings.set(false);
      },
      error: () => {
        this.aiSettingsMessage.set('Failed to load AI settings.');
        this.loadingAiSettings.set(false);
      }
    });
  }

  addAiProvider(): void {
    this.aiProviders.set([...this.aiProviders(), this.createProvider()]);
  }

  removeAiProvider(index: number): void {
    const removedKey = this.aiProviders()[index]?.key;
    this.aiProviders.set(this.aiProviders().filter((_, currentIndex) => currentIndex !== index));

    if (!removedKey) {
      return;
    }

    const remainingProviderKeys = new Set(this.aiProviders().map((provider) => provider.key));
    const fallbackProviderKey = this.aiProviders()[0]?.key ?? '';

    this.aiModels.set(
      this.aiModels()
        .filter((model) => model.providerKey !== removedKey)
        .map((model) => remainingProviderKeys.has(model.providerKey)
          ? model
          : { ...model, providerKey: fallbackProviderKey })
    );
  }

  addAiModel(): void {
    this.aiModels.set([...this.aiModels(), this.createModel()]);
  }

  removeAiModel(index: number): void {
    const next = this.aiModels().filter((_, currentIndex) => currentIndex !== index);
    if (next.length > 0 && !next.some((model) => model.isDefault)) {
      next[0] = { ...next[0], isDefault: true };
    }

    this.aiModels.set(next);
  }

  updateAiProviderField<K extends keyof AiProviderSettingsItem>(index: number, field: K, value: AiProviderSettingsItem[K]): void {
    const previousProvider = this.aiProviders()[index];
    this.aiProviders.set(this.updateItem(this.aiProviders(), index, (provider) => ({ ...provider, [field]: value })));

    if (field === 'providerType') {
      this.applyProviderTemplate(index, Number(value));
    }

    if (field === 'key') {
      const nextKey = String(value).trim();
      const previousKey = previousProvider?.key;
      if (!previousKey || previousKey === nextKey) {
        return;
      }

      this.aiModels.set(this.aiModels().map((model) =>
        model.providerKey === previousKey
          ? { ...model, providerKey: nextKey }
          : model));
    }
  }

  updateAiModelField<K extends keyof AiModelSettingsItem>(index: number, field: K, value: AiModelSettingsItem[K]): void {
    if (field === 'isDefault' && value) {
      this.aiModels.set(this.aiModels().map((model, currentIndex) => ({ ...model, isDefault: currentIndex === index })));
      return;
    }

    this.aiModels.set(this.updateItem(this.aiModels(), index, (model) => ({ ...model, [field]: value })));
  }

  saveAiSettings(): void {
    const providers = this.compactAiProviders(this.aiProviders());
    const models = this.compactAiModels(this.aiModels(), providers);

    if (providers.length === 0) {
      this.aiSettingsMessage.set('Add at least one enabled AI provider before saving.');
      return;
    }

    this.savingAiSettings.set(true);
    this.aiSettingsMessage.set('');

    this.videoService.updateAiSettings({
      providers,
      models,
      providerTypes: this.aiProviderTypes()
    }).subscribe({
      next: (settings) => {
        this.aiProviderTypes.set(settings.providerTypes ?? []);
        this.aiProviders.set(this.normalizeAiProviders(settings.providers ?? []));
        this.aiModels.set(this.normalizeAiModels(settings.models ?? []));
        this.aiSettingsMessage.set('AI provider and model settings saved.');
        this.savingAiSettings.set(false);
      },
      error: () => {
        this.aiSettingsMessage.set('Failed to save AI settings.');
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

  setActiveSection(section: 'ai' | 'agent' | 'video'): void {
    this.activeSection.set(section);
  }

  isActiveSection(section: 'ai' | 'agent' | 'video'): boolean {
    return this.activeSection() === section;
  }

  trackByIndex(index: number): number {
    return index;
  }

  trackByProviderType(_: number, providerType: AiProviderTypeOption): string {
    return providerType.key;
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

  private normalizeAiProviders(providers: AiProviderSettingsItem[]): AiProviderSettingsItem[] {
    return providers.length > 0 ? providers : [this.createProvider()];
  }

  private normalizeAiModels(models: AiModelSettingsItem[]): AiModelSettingsItem[] {
    const normalized = models.map((model) => ({
      ...model,
      temperature: model.temperature ?? null,
      maxOutputTokens: model.maxOutputTokens ?? null,
      configurationJson: model.configurationJson ?? ''
    }));

    if (normalized.length > 0 && !normalized.some((model) => model.isDefault)) {
      normalized[0] = { ...normalized[0], isDefault: true };
    }

    return normalized;
  }

  private compactAiProviders(providers: AiProviderSettingsItem[]): AiProviderSettingsItem[] {
    const seen = new Set<string>();

    return providers
      .map((provider) => ({
        ...provider,
        key: provider.key.trim(),
        name: provider.name.trim(),
        baseUrl: provider.baseUrl.trim(),
        apiKeyEnvironmentVariableName: provider.apiKeyEnvironmentVariableName.trim(),
        configurationJson: provider.configurationJson?.trim() ?? ''
      }))
      .filter((provider) => provider.key && provider.name && provider.baseUrl)
      .filter((provider) => {
        const key = provider.key.toLowerCase();
        if (seen.has(key)) {
          return false;
        }

        seen.add(key);
        return true;
      });
  }

  private compactAiModels(models: AiModelSettingsItem[], providers: AiProviderSettingsItem[]): AiModelSettingsItem[] {
    const providerKeys = new Set(providers.map((provider) => provider.key.toLowerCase()));
    const seen = new Set<string>();
    let defaultAssigned = false;

    const compact = models
      .map((model) => ({
        ...model,
        key: model.key.trim(),
        name: model.name.trim(),
        providerKey: model.providerKey.trim(),
        modelId: model.modelId.trim(),
        configurationJson: model.configurationJson?.trim() ?? '',
        temperature: this.toNullableNumber(model.temperature),
        maxOutputTokens: this.toNullableInteger(model.maxOutputTokens)
      }))
      .filter((model) => model.key && model.name && model.providerKey && model.modelId)
      .filter((model) => providerKeys.has(model.providerKey.toLowerCase()))
      .filter((model) => {
        const key = model.key.toLowerCase();
        if (seen.has(key)) {
          return false;
        }

        seen.add(key);
        return true;
      })
      .map((model) => {
        const isDefault = model.isDefault && !defaultAssigned;
        defaultAssigned = defaultAssigned || isDefault;
        return {
          ...model,
          isDefault
        };
      });

    if (compact.length > 0 && !compact.some((model) => model.isDefault)) {
      compact[0] = { ...compact[0], isDefault: true };
    }

    return compact;
  }

  private createProvider(): AiProviderSettingsItem {
    const template = this.aiProviderTypes()[0];
    const baseKey = template?.key ?? 'provider';
    return {
      id: 0,
      key: this.nextAvailableProviderKey(baseKey),
      name: template?.name ?? '',
      providerType: template?.value ?? 0,
      baseUrl: template?.defaultBaseUrl ?? '',
      apiKeyEnvironmentVariableName: template?.defaultApiKeyEnvironmentVariableName ?? '',
      isEnabled: true,
      configurationJson: ''
    };
  }

  private createModel(): AiModelSettingsItem {
    return {
      id: 0,
      key: '',
      name: '',
      providerKey: this.aiProviders()[0]?.key ?? '',
      modelId: '',
      isEnabled: true,
      isDefault: this.aiModels().length === 0,
      temperature: 0.2,
      maxOutputTokens: 1200,
      configurationJson: ''
    };
  }

  private applyProviderTemplate(index: number, providerTypeValue: number): void {
    const template = this.aiProviderTypes().find((item) => item.value === providerTypeValue);
    if (!template) {
      return;
    }

    this.aiProviders.set(this.updateItem(this.aiProviders(), index, (provider) => ({
      ...provider,
      name: provider.name || template.name,
      baseUrl: template.defaultBaseUrl,
      apiKeyEnvironmentVariableName: template.defaultApiKeyEnvironmentVariableName
    })));
  }

  private updateItem<T>(items: T[], index: number, updater: (item: T) => T): T[] {
    return items.map((item, currentIndex) => currentIndex === index ? updater(item) : item);
  }

  private nextAvailableProviderKey(baseKey: string): string {
    const normalizedBase = baseKey.trim() || 'provider';
    const existingKeys = new Set(this.aiProviders().map((provider) => provider.key.toLowerCase()));
    if (!existingKeys.has(normalizedBase.toLowerCase())) {
      return normalizedBase;
    }

    let counter = 2;
    while (existingKeys.has(`${normalizedBase}-${counter}`.toLowerCase())) {
      counter += 1;
    }

    return `${normalizedBase}-${counter}`;
  }

  private toNullableNumber(value: number | null | undefined): number | null {
    return value == null || !Number.isFinite(value) ? null : value;
  }

  private toNullableInteger(value: number | null | undefined): number | null {
    if (value == null || !Number.isFinite(value)) {
      return null;
    }

    const normalized = Math.floor(value);
    return normalized > 0 ? normalized : null;
  }
}
