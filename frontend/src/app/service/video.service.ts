import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../shared/api.constants';

export interface MediaSourceItem {
  id: number;
  path: string;
}

export interface MediaSyncCandidate {
  path: string;
  name: string;
  mediaType: 'movie' | 'episode';
  title?: string | null;
  year?: string | null;
  series?: string | null;
  season?: number | null;
  episode?: number | null;
}

export interface AiProviderTypeOption {
  value: number;
  key: string;
  name: string;
  defaultBaseUrl: string;
  defaultApiKeyEnvironmentVariableName: string;
  description: string;
}

export interface AiProviderSettingsItem {
  id: number;
  key: string;
  name: string;
  providerType: number;
  baseUrl: string;
  apiKeyEnvironmentVariableName: string;
  isEnabled: boolean;
  configurationJson?: string | null;
}

export interface AiModelSettingsItem {
  id: number;
  key: string;
  name: string;
  providerKey: string;
  modelId: string;
  isEnabled: boolean;
  isDefault: boolean;
  temperature?: number | null;
  maxOutputTokens?: number | null;
  configurationJson?: string | null;
}

export interface AiSettingsResponse {
  providers: AiProviderSettingsItem[];
  models: AiModelSettingsItem[];
  providerTypes: AiProviderTypeOption[];
}

export interface AiChatMessage {
  id?: number;
  role: 'user' | 'assistant' | 'system';
  content: string;
  modelKey?: string;
  modelName?: string;
  providerName?: string;
  createdAtUtc?: string;
}

export interface AiChatSessionSummary {
  id: number;
  title: string;
  modelKey: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AiChatSessionDetail extends AiChatSessionSummary {
  messages: AiChatMessage[];
}

export interface CreateAiChatSessionRequest {
  title?: string | null;
  modelKey?: string | null;
}

export interface StreamAiChatMessageRequest {
  modelKey?: string | null;
  content: string;
}

export interface AiChatStreamEvent {
  type: 'session' | 'chunk' | 'completed';
  session?: AiChatSessionSummary;
  content?: string;
  message?: AiChatMessage;
}

export interface ExpenseGroup {
  id: number;
  key: string;
  name: string;
  color: string;
  isEnabled: boolean;
  displayOrder: number;
}

export interface ExpenseModel {
  key: string;
  name: string;
  modelId: string;
  providerName: string;
  isDefault: boolean;
}

export interface ExpenseSettings {
  modelKey: string | null;
  defaultCurrencyCode: string;
  extractionPrompt?: string | null;
}

export interface ExpenseEntry {
  id: number;
  expenseGroupId?: number | null;
  expenseGroupName?: string | null;
  expenseGroupColor?: string | null;
  sourceType: string;
  expenseDate: string;
  totalAmount: number;
  subtotalAmount?: number | null;
  taxAmount?: number | null;
  tipAmount?: number | null;
  currencyCode: string;
  merchantName?: string | null;
  location?: string | null;
  paymentMethod?: string | null;
  description?: string | null;
  notes?: string | null;
  receiptFileName?: string | null;
  modelKeyUsed?: string | null;
  analysisConfidence?: number | null;
  receiptText?: string | null;
  isWorkExpense: boolean;
  isReimbursable: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ExpenseDetailResponse {
  expense: ExpenseEntry;
  rawExtractionJson?: string | null;
  receiptImageDataUrl?: string | null;
  receiptImageContentType?: string | null;
  items: ExpenseLineItem[];
}

export interface ExpenseLineItem {
  id: number;
  name: string;
  canonicalName: string;
  quantity?: number | null;
  unit?: string | null;
  unitPrice?: number | null;
  totalPrice: number;
}

export interface ExpenseItemPurchase extends ExpenseLineItem {
  expenseId: number;
  comparablePrice?: number | null;
  currencyCode: string;
  merchantName?: string | null;
  location?: string | null;
  expenseDate: string;
}

export interface ExpenseItemComparison {
  name: string;
  normalizedName: string;
  purchaseCount: number;
  lowestPrice?: number | null;
  lowestPriceMerchant?: string | null;
  purchases: ExpenseItemPurchase[];
}

export interface ExpenseItemReclassification {
  canonicalName: string;
  normalizedName: string;
  updatedCount: number;
}

export interface ExpenseGroupTotal {
  expenseGroupId?: number | null;
  name: string;
  color: string;
  totalAmount: number;
  expenseCount: number;
}

export interface ExpenseSummary {
  totalAmount: number;
  expenseCount: number;
  averageAmount: number;
  latestExpenseDate?: string | null;
  totalsByGroup: ExpenseGroupTotal[];
}

export interface ExpenseDashboardResponse {
  expenses: ExpenseEntry[];
  groups: ExpenseGroup[];
  settings: ExpenseSettings;
  models: ExpenseModel[];
  summary: ExpenseSummary;
}

export interface ExpenseReceiptExtraction {
  classificationGroupKey?: string | null;
  expenseDate?: string | null;
  totalAmount?: number | null;
  subtotalAmount?: number | null;
  taxAmount?: number | null;
  tipAmount?: number | null;
  currencyCode?: string | null;
  merchantName?: string | null;
  location?: string | null;
  paymentMethod?: string | null;
  description?: string | null;
  notes?: string | null;
  receiptText?: string | null;
  confidence?: number | null;
  items: Array<{
    name?: string | null;
    quantity?: number | null;
    unit?: string | null;
    unitPrice?: number | null;
    totalPrice?: number | null;
  }>;
}

export interface AnalyzeExpenseResponse {
  expense: ExpenseEntry;
  extraction: ExpenseReceiptExtraction;
}

export interface ExpenseCsvImportReportItem {
  rowNumber: number;
  status: 'added' | 'duplicate' | 'review' | 'failed' | string;
  message?: string | null;
  merchantName?: string | null;
  amount?: number | null;
  currencyCode?: string | null;
  expenseDate?: string | null;
  expenseGroupName?: string | null;
  schemaKey?: string | null;
  expense?: ExpenseEntry | null;
  rawColumns?: Record<string, string> | null;
  approvalPayload?: ExpenseImportApprovalPayload | null;
}

export interface ExpenseImportApprovalPayload {
  rowNumber: number;
  sourceType: string;
  importFileName: string;
  schemaKey: string;
  expenseDate: string;
  totalAmount: number;
  currencyCode: string;
  merchantName?: string | null;
  description?: string | null;
  notes?: string | null;
  paymentMethod?: string | null;
  location?: string | null;
  sourceReference?: string | null;
  expenseGroupId?: number | null;
  expenseGroupName?: string | null;
  modelKeyUsed?: string | null;
  analysisConfidence?: number | null;
  rawColumns?: Record<string, string> | null;
}

export interface ExpenseCsvImportResponse {
  schemaKey: string;
  schemaDisplayName: string;
  totalRows: number;
  addedCount: number;
  duplicateCount: number;
  reviewCount: number;
  failedCount: number;
  items: ExpenseCsvImportReportItem[];
}

export interface ImportExpenseFetchRequest {
  modelKey?: string | null;
  requestText: string;
}

export interface ApproveExpenseImportRowsRequest {
  schemaKey?: string | null;
  schemaDisplayName?: string | null;
  rows: ExpenseImportApprovalPayload[];
}

export interface ExpensePreviewResponse {
  expenseGroupId?: number | null;
  expenseDate: string;
  totalAmount?: number | null;
  subtotalAmount?: number | null;
  taxAmount?: number | null;
  tipAmount?: number | null;
  currencyCode: string;
  merchantName?: string | null;
  location?: string | null;
  paymentMethod?: string | null;
  description?: string | null;
  notes?: string | null;
  receiptText?: string | null;
  modelKeyUsed: string;
  extraction: ExpenseReceiptExtraction;
}

export interface CreateManualExpenseRequest {
  expenseGroupId?: number | null;
  expenseDate?: string;
  totalAmount: number;
  subtotalAmount?: number;
  taxAmount?: number;
  tipAmount?: number;
  currencyCode?: string;
  merchantName?: string;
  location?: string;
  paymentMethod?: string;
  description?: string;
  notes?: string;
}

export interface UpdateExpenseRequest extends CreateManualExpenseRequest {
  receiptText?: string;
  isWorkExpense: boolean;
  isReimbursable: boolean;
}

export interface ExpenseSettingsSaveRequest {
  settings: ExpenseSettings;
  groups: ExpenseGroup[];
}

export interface MediaSourcesResponse {
  movieSources: MediaSourceItem[];
  tvSources: MediaSourceItem[];
}

export interface TvEpisode {
  id?: number;
  title: string;
  description?: string;
  season: number;
  episodeNumber: number;
  filePath: string;
  videoUrl?: string;
  thumbnail?: string;
  thumbnailUrl?: string;
  hidden?: boolean;
  watched?: boolean;
}

export interface TvSeriesListItem {
  id?: number;
  name?: string;
  title: string;
  description?: string;
  genre?: string;
  thumbnail?: string;
  thumbnailUrl?: string;
  seasonCount: number;
  episodeCount: number;
  watchedEpisodeCount: number;
  fullyWatched: boolean;
}

export interface PlaybackOptions {
  browserUrl: string;
  homeUrl?: string;
  homeAvailable: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class VideoService {
  private readonly apiUrl = buildApiUrl('/video');
  private readonly apiController = buildApiUrl('/video/control');
  private readonly tvUrl = buildApiUrl('/tv');
  private readonly settingsUrl = buildApiUrl('/settings/media-sources');
  private readonly aiSettingsUrl = buildApiUrl('/settings/ai');
  private readonly aiChatUrl = buildApiUrl('/ai/chat');
  private readonly expensesUrl = buildApiUrl('/expenses');

  constructor(private http: HttpClient) {}

  getMovies(): Observable<Record<string, any[]>> {
    return this.http.get<Record<string, any[]>>(this.apiUrl);
  }

  getMoviesSample(limit: number): Observable<Record<string, any[]>> {
    return this.http.get<Record<string, any[]>>(`${this.apiUrl}?sample=${limit}`);
  }

  getMoviesByGenre(genre: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}?genre=${encodeURIComponent(genre)}`);
  }

  getMoviesByGenrePaged(genre: string, page: number, pageSize: number, search?: string): Observable<any> {
    const params = new URLSearchParams({
      genre,
      page: String(page),
      pageSize: String(pageSize)
    });
    if (search) {
      params.set('search', search);
    }
    return this.http.get<any>(`${this.apiUrl}/genre?${params.toString()}`);
  }

  getAllMoviesList(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/list`);
  }

  getPlaybackOptions(filePath: string): Observable<PlaybackOptions> {
    const params = new URLSearchParams({ filePath });
    return this.http.get<PlaybackOptions>(`${this.apiUrl}/playback-options?${params.toString()}`);
  }

  searchMovies(query: string, maxResults: number = 100): Observable<Record<string, any[]>> {
    const params = new URLSearchParams({
      query,
      maxResults: String(maxResults)
    });
    return this.http.get<Record<string, any[]>>(`${this.apiUrl}/search?${params.toString()}`);
  }

  updateMovie(id: number, payload: { title?: string; year?: string; genre?: string; description?: string; plot?: string }): Observable<any> {
    return this.http.patch(`${this.apiUrl}/${id}`, payload);
  }

  resyncMovie(id: number): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/resync`, {});
  }

  getSeries(): Observable<Record<string, any[]>> {
    return this.http.get<Record<string, any[]>>(this.tvUrl);
  }

  getSeriesSample(limit: number): Observable<Record<string, any[]>> {
    return this.http.get<Record<string, any[]>>(`${this.tvUrl}?sample=${limit}`);
  }

  getSeriesByGenre(genre: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.tvUrl}?genre=${encodeURIComponent(genre)}`);
  }

  getSeriesEpisodes(seriesId: number): Observable<TvEpisode[]> {
    return this.http.get<TvEpisode[]>(`${this.tvUrl}/${seriesId}/episodes`);
  }

  getAllSeriesList(): Observable<TvSeriesListItem[]> {
    return this.http.get<TvSeriesListItem[]>(`${this.tvUrl}/list`);
  }

  searchSeries(query: string, maxResults: number = 100): Observable<Record<string, any[]>> {
    const params = new URLSearchParams({
      query,
      maxResults: String(maxResults)
    });
    return this.http.get<Record<string, any[]>>(`${this.tvUrl}/search?${params.toString()}`);
  }

  updateSeries(id: number, payload: { title?: string; description?: string; genre?: string; thumbnail?: string }): Observable<any> {
    return this.http.patch(`${this.tvUrl}/series/${id}`, payload);
  }

  resyncSeries(id: number): Observable<any> {
    return this.http.post(`${this.tvUrl}/series/${id}/resync`, {});
  }

  resyncSeason(seriesId: number, season: number): Observable<any> {
    return this.http.post(`${this.tvUrl}/series/${seriesId}/season/${season}/resync`, {});
  }

  getMovieSyncCandidates(): Observable<MediaSyncCandidate[]> {
    return this.http.get<MediaSyncCandidate[]>(`${this.apiUrl}/sync/candidates`);
  }

  getTvSyncCandidates(): Observable<MediaSyncCandidate[]> {
    return this.http.get<MediaSyncCandidate[]>(`${this.tvUrl}/sync/candidates`);
  }

  syncMovies(limit?: number, selectedPaths?: string[]): Observable<any> {
    const query = limit && limit > 0 ? `?limit=${limit}` : '';
    return this.http.post(`${this.apiUrl}/sync${query}`, {
      selectedPaths: selectedPaths ?? null
    });
  }

  syncTv(limit?: number, selectedPaths?: string[]): Observable<any> {
    const query = limit && limit > 0 ? `?limit=${limit}` : '';
    return this.http.post(`${this.tvUrl}/sync${query}`, {
      selectedPaths: selectedPaths ?? null
    });
  }

  getMediaSourceSettings(): Observable<MediaSourcesResponse> {
    return this.http.get<MediaSourcesResponse>(this.settingsUrl);
  }

  updateMediaSourceSettings(payload: MediaSourcesResponse): Observable<MediaSourcesResponse> {
    return this.http.put<MediaSourcesResponse>(this.settingsUrl, payload);
  }

  getAiSettings(): Observable<AiSettingsResponse> {
    return this.http.get<AiSettingsResponse>(this.aiSettingsUrl);
  }

  updateAiSettings(payload: AiSettingsResponse): Observable<AiSettingsResponse> {
    return this.http.put<AiSettingsResponse>(this.aiSettingsUrl, payload);
  }

  getExpenses(filters?: {
    search?: string;
    expenseGroupId?: number;
    dateFrom?: string;
    dateTo?: string;
    sourceType?: string;
  }): Observable<ExpenseDashboardResponse> {
    const params = new URLSearchParams();
    if (filters?.search) {
      params.set('search', filters.search);
    }
    if (typeof filters?.expenseGroupId === 'number') {
      params.set('expenseGroupId', String(filters.expenseGroupId));
    }
    if (filters?.dateFrom) {
      params.set('dateFrom', filters.dateFrom);
    }
    if (filters?.dateTo) {
      params.set('dateTo', filters.dateTo);
    }
    if (filters?.sourceType) {
      params.set('sourceType', filters.sourceType);
    }

    const query = params.toString();
    return this.http.get<ExpenseDashboardResponse>(query ? `${this.expensesUrl}?${query}` : this.expensesUrl);
  }

  saveExpenseSettings(payload: ExpenseSettingsSaveRequest): Observable<ExpenseDashboardResponse> {
    return this.http.put<ExpenseDashboardResponse>(`${this.expensesUrl}/settings`, payload);
  }

  createManualExpense(payload: CreateManualExpenseRequest): Observable<ExpenseEntry> {
    return this.http.post<ExpenseEntry>(`${this.expensesUrl}/manual`, payload);
  }

  getExpense(expenseId: number): Observable<ExpenseDetailResponse> {
    return this.http.get<ExpenseDetailResponse>(`${this.expensesUrl}/${expenseId}`);
  }

  searchExpenseItems(search?: string): Observable<ExpenseItemComparison[]> {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
    return this.http.get<ExpenseItemComparison[]>(`${this.expensesUrl}/items${query}`);
  }

  reclassifyExpenseItem(itemId: number, canonicalName: string, applyToMatching = true): Observable<ExpenseItemReclassification> {
    return this.http.put<ExpenseItemReclassification>(`${this.expensesUrl}/items/${itemId}/classification`, {
      canonicalName,
      applyToMatching
    });
  }

  updateExpense(expenseId: number, payload: UpdateExpenseRequest): Observable<ExpenseEntry> {
    return this.http.put<ExpenseEntry>(`${this.expensesUrl}/${expenseId}`, payload);
  }

  analyzeExpensePreview(payload: Record<string, string>, file?: File | null): Observable<ExpensePreviewResponse> {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => formData.append(key, value));
    if (file) {
      formData.append('file', file, file.name);
    }

    return this.http.post<ExpensePreviewResponse>(`${this.expensesUrl}/analyze-preview`, formData);
  }

  analyzeExpense(payload: Record<string, string>, file?: File | null): Observable<AnalyzeExpenseResponse> {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => formData.append(key, value));
    if (file) {
      formData.append('file', file, file.name);
    }

    return this.http.post<AnalyzeExpenseResponse>(`${this.expensesUrl}/analyze`, formData);
  }

  importExpenseCsv(modelKey: string | null | undefined, file: File): Observable<ExpenseCsvImportResponse> {
    const formData = new FormData();
    if (modelKey) {
      formData.append('modelKey', modelKey);
    }

    formData.append('file', file, file.name);
    return this.http.post<ExpenseCsvImportResponse>(`${this.expensesUrl}/import-csv`, formData);
  }

  importExpenseFetch(payload: ImportExpenseFetchRequest): Observable<ExpenseCsvImportResponse> {
    return this.http.post<ExpenseCsvImportResponse>(`${this.expensesUrl}/import-fetch`, payload);
  }

  approveExpenseImportRows(payload: ApproveExpenseImportRowsRequest): Observable<ExpenseCsvImportResponse> {
    return this.http.post<ExpenseCsvImportResponse>(`${this.expensesUrl}/import-approve`, payload);
  }

  getAiChatSessions(): Observable<AiChatSessionSummary[]> {
    return this.http.get<AiChatSessionSummary[]>(`${this.aiChatUrl}/sessions`);
  }

  getAiChatSession(sessionId: number): Observable<AiChatSessionDetail> {
    return this.http.get<AiChatSessionDetail>(`${this.aiChatUrl}/sessions/${sessionId}`);
  }

  createAiChatSession(payload: CreateAiChatSessionRequest): Observable<AiChatSessionDetail> {
    return this.http.post<AiChatSessionDetail>(`${this.aiChatUrl}/sessions`, payload);
  }

  async streamAiChatMessage(
    sessionId: number | null,
    payload: StreamAiChatMessageRequest,
    onEvent: (event: AiChatStreamEvent) => void
  ): Promise<void> {
    const url = sessionId == null
      ? `${this.aiChatUrl}/messages/stream`
      : `${this.aiChatUrl}/sessions/${sessionId}/messages/stream`;

    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok || !response.body) {
      throw new Error(`Streaming request failed with status ${response.status}`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const result = await reader.read();
      if (result.done) {
        break;
      }

      buffer += decoder.decode(result.value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';

      for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed) {
          continue;
        }

        onEvent(JSON.parse(trimmed) as AiChatStreamEvent);
      }
    }

    const trailing = buffer.trim();
    if (trailing) {
      onEvent(JSON.parse(trailing) as AiChatStreamEvent);
    }
  }

  playServerVideo(path?: string): void {
    this.sendCommand('play', path).subscribe();
  }

  sendCommand(command: string, path?: string, seconds?: number): Observable<any> {
    return this.http.post(this.apiController, { command, path, seconds });
  }

  setEpisodeWatched(id: number, watched: boolean): Observable<any> {
    return this.http.patch(`${this.tvUrl}/${id}/watched`, { watched });
  }

  setMovieWatched(id: number, watched: boolean): Observable<any> {
    return this.http.patch(`${this.apiUrl}/${id}/watched`, { watched });
  }
}
