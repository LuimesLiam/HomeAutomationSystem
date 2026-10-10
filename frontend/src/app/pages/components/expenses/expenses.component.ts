import { DatePickerModule } from 'primeng/datepicker';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import {
  ApproveExpenseImportRowsRequest,
  AnalyzeExpenseResponse,
  CreateManualExpenseRequest,
  ExpenseCsvImportResponse,
  ExpenseImportApprovalPayload,
  ExpenseItemComparison,
  ExpenseDashboardResponse,
  ExpenseDetailResponse,
  ExpenseEntry,
  ExpenseGroup,
  ExpensePreviewResponse,
  ExpenseSettings,
  ExpenseSettingsSaveRequest,
  UpdateExpenseRequest,
  VideoService
} from '../../../service/video.service';

type ExpenseSourceFilter = '' | 'manual' | 'receipt' | 'csv-import' | 'fetch-import';
type ExpenseSection = 'capture' | 'history' | 'items' | 'report' | 'settings';
type ExpenseRequestFeedback = {
  tone: 'pending' | 'success' | 'error';
  message: string;
};
type ToastSeverity = 'success' | 'info' | 'warn' | 'error';
type DashboardLoadOptions = {
  successToast?: string;
  errorToast?: string;
  silentSuccess?: boolean;
};
type ReportDailyTotal = {
  day: number;
  label: string;
  totalAmount: number;
  expenseCount: number;
};
type ReportGroupShare = {
  name: string;
  color: string;
  totalAmount: number;
  expenseCount: number;
  percent: number;
};
type ReportWeekdayTotal = {
  label: string;
  totalAmount: number;
  expenseCount: number;
};
type ReportMerchantInsight = {
  merchantName: string;
  totalAmount: number;
  expenseCount: number;
};
type ReportGroupDrilldown = {
  name: string;
  color: string;
};

@Component({
  selector: 'app-expenses',
  templateUrl: './expenses.component.html',
  styleUrls: ['./expenses.component.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, DatePickerModule]
})
export class ExpensesComponent implements OnInit {
  readonly reportMonthOptions = [
    { value: '01', label: 'January' },
    { value: '02', label: 'February' },
    { value: '03', label: 'March' },
    { value: '04', label: 'April' },
    { value: '05', label: 'May' },
    { value: '06', label: 'June' },
    { value: '07', label: 'July' },
    { value: '08', label: 'August' },
    { value: '09', label: 'September' },
    { value: '10', label: 'October' },
    { value: '11', label: 'November' },
    { value: '12', label: 'December' }
  ];
  readonly expenseSections: ExpenseSection[] = ['capture', 'history', 'items', 'report', 'settings'];
  readonly activeSection = signal<ExpenseSection>('capture');
  readonly loading = signal(false);
  readonly savingManual = signal(false);
  readonly autofillingExpense = signal(false);
  readonly analyzingReceipt = signal(false);
  readonly importingCsv = signal(false);
  readonly importingFetch = signal(false);
  readonly approvingImportRows = signal(false);
  readonly savingSettings = signal(false);
  readonly loadingExpenseDetail = signal(false);
  readonly savingExpenseDetail = signal(false);
  readonly pageMessage = signal('');
  readonly captureFeedback = signal<ExpenseRequestFeedback | null>(null);
  readonly csvImportFeedback = signal<ExpenseRequestFeedback | null>(null);
  readonly fetchImportFeedback = signal<ExpenseRequestFeedback | null>(null);
  readonly detailFeedback = signal<ExpenseRequestFeedback | null>(null);
  readonly historyFeedback = signal<ExpenseRequestFeedback | null>(null);
  readonly itemSearch = signal('');
  readonly itemComparisons = signal<ExpenseItemComparison[]>([]);
  readonly loadingItems = signal(false);
  readonly reclassifyingItem = signal(false);
  readonly itemFeedback = signal<ExpenseRequestFeedback | null>(null);
  readonly selectedFileName = signal('');
  readonly selectedCsvFileName = signal('');
  readonly fetchImportDialogOpen = signal(false);
  readonly fetchImportRequestText = signal('');
  readonly showFetchImportReport = signal(false);
  readonly csvImportReport = signal<ExpenseCsvImportResponse | null>(null);
  readonly selectedImportApprovalKeys = signal<string[]>([]);
  readonly expenseEntries = signal<ExpenseEntry[]>([]);
  readonly expenseGroups = signal<ExpenseGroup[]>([]);
  readonly settings = signal<ExpenseSettings>({ modelKey: null, defaultCurrencyCode: 'USD', extractionPrompt: '' });
  readonly availableModels = signal<ExpenseDashboardResponse['models']>([]);
  readonly summary = signal<ExpenseDashboardResponse['summary']>({
    totalAmount: 0,
    expenseCount: 0,
    averageAmount: 0,
    latestExpenseDate: null,
    totalsByGroup: []
  });
  readonly filterSearch = signal('');
  readonly filterExpenseGroupId = signal<number | null>(null);
  readonly filterDateFrom = signal('');
  readonly filterDateTo = signal('');
  readonly filterSourceType = signal<ExpenseSourceFilter>('');
  readonly reportMonth = signal(this.currentMonth());
  readonly reportDayFrom = signal('');
  readonly reportDayTo = signal('');
  readonly reportExcludeReimbursable = signal(true);
  readonly expenseForm = signal({
    expenseGroupId: null as number | null,
    expenseDate: this.today(),
    totalAmount: '',
    subtotalAmount: '',
    taxAmount: '',
    tipAmount: '',
    currencyCode: 'USD',
    merchantName: '',
    location: '',
    paymentMethod: '',
    description: '',
    notes: '',
    receiptText: ''
  });
  readonly editableGroups = signal<ExpenseGroup[]>([]);
  readonly selectedExpenseDetail = signal<ExpenseDetailResponse | null>(null);
  readonly selectedReportGroup = signal<ReportGroupDrilldown | null>(null);
  readonly editingExpenseForm = signal({
    id: 0,
    expenseGroupId: null as number | null,
    expenseDate: '',
    totalAmount: '',
    subtotalAmount: '',
    taxAmount: '',
    tipAmount: '',
    currencyCode: 'USD',
    merchantName: '',
    location: '',
    paymentMethod: '',
    description: '',
    notes: '',
    receiptText: '',
    isWorkExpense: false,
    isReimbursable: false
  });
  private selectedFile: File | null = null;
  private selectedCsvFile: File | null = null;

  constructor(
    private videoService: VideoService,
    private route: ActivatedRoute,
    private router: Router,
    private messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      const section = params.get('section');
      const group = params.get('group');

      if (section === 'capture' || section === 'history' || section === 'items' || section === 'report' || section === 'settings') {
        this.activeSection.set(section);
        if (section === 'items') {
          this.loadItems();
        }
      }

      if (!group || group === 'all') {
        this.filterExpenseGroupId.set(null);
        return;
      }

      const matchedGroup = this.expenseGroups().find((item) => item.key === group || String(item.id) === group);
      this.filterExpenseGroupId.set(matchedGroup?.id ?? null);
    });

    this.loadDashboard({ silentSuccess: true });
  }

  setItemSearch(value: string): void {
    this.itemSearch.set(value);
  }

  loadItems(): void {
    this.loadingItems.set(true);
    this.itemFeedback.set({ tone: 'pending', message: 'Looking through item prices...' });
    this.videoService.searchExpenseItems(this.itemSearch()).subscribe({
      next: (items) => {
        this.itemComparisons.set(items);
        this.loadingItems.set(false);
        this.itemFeedback.set(null);
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'Item prices could not be loaded.');
        this.loadingItems.set(false);
        this.itemFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Price search failed', message);
      }
    });
  }

  itemPriceLabel(item: ExpenseItemComparison['purchases'][number]): string {
    const price = item.comparablePrice ?? item.totalPrice;
    const suffix = item.quantity && item.quantity !== 1
      ? ` each${item.unit ? ` / ${item.unit}` : ''}`
      : item.unit ? ` / ${item.unit}` : '';
    return `${this.formatMoney(price, item.currencyCode)}${suffix}`;
  }

  reclassifyComparison(item: ExpenseItemComparison, targetKey: string): void {
    let canonicalName = this.itemComparisons().find((candidate) => candidate.normalizedName === targetKey)?.name;
    if (targetKey === '__new__') {
      canonicalName = window.prompt('Enter the canonical product name. Similar future receipts will reuse it.')?.trim();
    }

    if (!canonicalName || targetKey === item.normalizedName) {
      return;
    }

    const sourceItemId = item.purchases[0]?.id;
    if (!sourceItemId) {
      return;
    }

    this.reclassifyingItem.set(true);
    this.itemFeedback.set({ tone: 'pending', message: `Reclassifying ${item.name} as ${canonicalName}...` });
    this.videoService.reclassifyExpenseItem(sourceItemId, canonicalName, true).subscribe({
      next: (result) => {
        this.reclassifyingItem.set(false);
        this.itemFeedback.set({ tone: 'success', message: `${result.updatedCount} item entr${result.updatedCount === 1 ? 'y' : 'ies'} reclassified as ${result.canonicalName}.` });
        this.showToast('success', 'Items merged', `Future receipts can now reuse ${result.canonicalName}.`);
        this.loadItems();
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'The item could not be reclassified.');
        this.reclassifyingItem.set(false);
        this.itemFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Reclassification failed', message);
      }
    });
  }

  setActiveSection(section: ExpenseSection): void {
    if (section === 'report') {
      this.initializeReportFilters();
      this.applyReportFilters();
      return;
    }

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        section,
        group: section === 'history' ? this.activeHistoryGroupKey() : null
      },
      queryParamsHandling: 'merge'
    });
  }

  isActiveSection(section: ExpenseSection): boolean {
    return this.activeSection() === section;
  }

  setHistoryGroup(groupKey: string | null): void {
    const matchedGroup = groupKey
      ? this.expenseGroups().find((group) => group.key === groupKey)
      : null;

    this.filterExpenseGroupId.set(matchedGroup?.id ?? null);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        section: 'history',
        group: groupKey ?? 'all'
      },
      queryParamsHandling: 'merge'
    });
    this.loadDashboard({
      successToast: matchedGroup?.name ? `Showing ${matchedGroup.name} expenses.` : 'Showing all expenses.'
    });
  }

  isActiveHistoryGroup(groupKey: string | null): boolean {
    return this.activeHistoryGroupKey() === (groupKey ?? 'all');
  }

  activeHistoryGroupKey(): string {
    const selectedId = this.filterExpenseGroupId();
    if (selectedId == null) {
      return 'all';
    }

    return this.expenseGroups().find((group) => group.id === selectedId)?.key ?? 'all';
  }

  loadDashboard(options?: DashboardLoadOptions): void {
    this.loading.set(true);
    this.pageMessage.set('');
    this.historyFeedback.set({ tone: 'pending', message: 'Refreshing expense history...' });

    this.videoService.getExpenses({
      search: this.filterSearch() || undefined,
      expenseGroupId: this.filterExpenseGroupId() ?? undefined,
      dateFrom: this.filterDateFrom() || undefined,
      dateTo: this.filterDateTo() || undefined,
      sourceType: this.filterSourceType() || undefined
    }).subscribe({
      next: (response) => {
        this.expenseEntries.set(response.expenses ?? []);
        this.expenseGroups.set(response.groups ?? []);
        this.availableModels.set(response.models ?? []);
        this.settings.set(response.settings ?? this.settings());
        this.summary.set(response.summary ?? this.summary());
        this.editableGroups.set((response.groups ?? []).map((group) => ({ ...group })));
        this.syncRouteGroupSelection();
        this.syncCurrencyDefault();
        this.loading.set(false);
        this.historyFeedback.set(null);
        if (!options?.silentSuccess) {
          this.showToast('success', 'Expenses updated', options?.successToast || 'Expense data refreshed.');
        }
      },
      error: () => {
        this.pageMessage.set('Failed to load expenses.');
        this.loading.set(false);
        this.historyFeedback.set({ tone: 'error', message: 'History could not be loaded.' });
        this.showToast('error', 'Refresh failed', options?.errorToast || 'Expense data could not be loaded.');
      }
    });
  }

  applyFilters(): void {
    if (this.activeSection() === 'history') {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          section: 'history',
          group: this.activeHistoryGroupKey()
        },
        queryParamsHandling: 'merge'
      });
    }

    this.loadDashboard({ successToast: 'History filters applied.' });
  }

  clearFilters(): void {
    this.filterSearch.set('');
    this.filterDateFrom.set('');
    this.filterDateTo.set('');
    this.filterSourceType.set('');
    this.setHistoryGroup(null);
  }

  setReportMonth(value: string): void {
    this.reportMonth.set(value || this.currentMonth());
  }

  setReportMonthName(monthValue: string): void {
    const year = this.reportYearOptions().includes(this.reportYear()) ? this.reportYear() : this.currentMonth().slice(0, 4);
    this.setReportMonth(`${year}-${monthValue || this.currentMonth().slice(5, 7)}`);
  }

  setReportYear(value: string): void {
    const month = this.reportMonthValue();
    this.setReportMonth(`${value || this.currentMonth().slice(0, 4)}-${month}`);
  }

  shiftReportMonth(offset: number): void {
    const [yearString, monthString] = (this.reportMonth() || this.currentMonth()).split('-');
    const currentDate = new Date(Date.UTC(Number(yearString), Number(monthString) - 1 + offset, 1));
    const year = currentDate.getUTCFullYear();
    const month = String(currentDate.getUTCMonth() + 1).padStart(2, '0');
    this.setReportMonth(`${year}-${month}`);
  }

  setReportDayFrom(value: string): void {
    this.reportDayFrom.set(value);
  }

  setReportDayTo(value: string): void {
    this.reportDayTo.set(value);
  }

  setReportExcludeReimbursable(value: boolean): void {
    this.reportExcludeReimbursable.set(value);
  }

  setManualExpenseDate(value: string | null | undefined): void {
    this.updateForm('expenseDate', value || this.today());
  }

  applyReportFilters(): void {
    const month = this.reportMonth() || this.currentMonth();
    const [yearString, monthString] = month.split('-');
    const year = Number(yearString);
    const monthIndex = Number(monthString);

    if (!Number.isInteger(year) || !Number.isInteger(monthIndex) || monthIndex < 1 || monthIndex > 12) {
      this.pageMessage.set('Choose a valid report month.');
      this.showToast('warn', 'Invalid report month', 'Pick a valid month before refreshing the report.');
      return;
    }

    const totalDays = this.daysInMonth(month);
    const rawDayFrom = Number(this.reportDayFrom());
    const rawDayTo = Number(this.reportDayTo());
    const hasDayFrom = Number.isFinite(rawDayFrom) && rawDayFrom > 0;
    const hasDayTo = Number.isFinite(rawDayTo) && rawDayTo > 0;
    const normalizedDayFrom = hasDayFrom ? Math.min(Math.max(Math.trunc(rawDayFrom), 1), totalDays) : 1;
    const normalizedDayTo = hasDayTo ? Math.min(Math.max(Math.trunc(rawDayTo), normalizedDayFrom), totalDays) : totalDays;

    this.reportDayFrom.set(hasDayFrom ? String(normalizedDayFrom) : '');
    this.reportDayTo.set(hasDayTo ? String(normalizedDayTo) : '');
    this.filterDateFrom.set(this.buildMonthDate(month, normalizedDayFrom));
    this.filterDateTo.set(this.buildMonthDate(month, normalizedDayTo));
    this.activeSection.set('report');

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        section: 'report',
        group: this.activeHistoryGroupKey()
      },
      queryParamsHandling: 'merge'
    });

    this.loadDashboard({ successToast: 'Report refreshed.' });
  }

  resetReportFilters(): void {
    this.reportMonth.set(this.currentMonth());
    this.reportDayFrom.set('');
    this.reportDayTo.set('');
    this.reportExcludeReimbursable.set(true);
    this.applyReportFilters();
  }

  setFilterSearch(value: string): void {
    this.filterSearch.set(value);
  }

  setFilterExpenseGroupId(value: string): void {
    this.filterExpenseGroupId.set(value ? Number(value) : null);
  }

  setFilterDateFrom(value: string): void {
    this.filterDateFrom.set(value);
  }

  setFilterDateTo(value: string): void {
    this.filterDateTo.set(value);
  }

  setFilterSourceType(value: string): void {
    this.filterSourceType.set((value || '') as ExpenseSourceFilter);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;
    this.selectedFile = file;
    this.selectedFileName.set(file?.name ?? '');
  }

  onCsvFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;
    this.selectedCsvFile = file;
    this.selectedCsvFileName.set(file?.name ?? '');
  }

  updateForm<K extends keyof ReturnType<ExpensesComponent['expenseForm']>>(field: K, value: ReturnType<ExpensesComponent['expenseForm']>[K]): void {
    this.expenseForm.set({
      ...this.expenseForm(),
      [field]: value
    });
  }

  saveManualExpense(): void {
    const payload = this.buildManualPayload();
    if (!payload) {
      this.pageMessage.set('Choose a classification group and enter a total amount before saving a manual expense.');
      this.showToast('warn', 'Manual entry incomplete', 'Classification group and total are required.');
      return;
    }

    this.savingManual.set(true);
    this.pageMessage.set('');
    this.captureFeedback.set({ tone: 'pending', message: 'Saving manual expense...' });

    this.videoService.createManualExpense(payload).subscribe({
      next: () => {
        this.pageMessage.set('Manual expense saved.');
        this.captureFeedback.set({ tone: 'success', message: 'Manual expense saved successfully.' });
        this.showToast('success', 'Manual entry saved', 'The expense was added successfully.');
        this.resetExpenseForm();
        this.loadDashboard({ silentSuccess: true });
        this.savingManual.set(false);
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'Manual expense could not be saved.');
        this.pageMessage.set('Failed to save the manual expense.');
        this.savingManual.set(false);
        this.captureFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Manual save failed', message);
      }
    });
  }

  autofillFromReceipt(): void {
    if (!this.selectedFile && !this.expenseForm().receiptText.trim()) {
      this.pageMessage.set('Choose a receipt image or paste receipt text before using auto fill.');
      this.showToast('warn', 'Receipt needed', 'Add a receipt image or pasted receipt text before auto fill.');
      return;
    }

    this.autofillingExpense.set(true);
    this.pageMessage.set('');
    this.captureFeedback.set({ tone: 'pending', message: 'Asking the model to auto-fill the expense...' });

    this.videoService.analyzeExpensePreview(this.buildAnalyzePayload(), this.selectedFile).subscribe({
      next: (preview: ExpensePreviewResponse) => {
        this.applyPreviewToForm(preview);
        this.pageMessage.set('Fields were auto-filled. Review them before saving.');
        this.autofillingExpense.set(false);
        this.captureFeedback.set({ tone: 'success', message: 'Auto fill completed. Review the populated fields before saving.' });
        this.showToast('success', 'Auto fill complete', 'Review the populated fields before saving.');
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'Auto fill failed.');
        this.pageMessage.set(message);
        this.autofillingExpense.set(false);
        this.captureFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Auto fill failed', message);
      }
    });
  }

  analyzeReceipt(): void {
    if (!this.selectedFile && !this.expenseForm().receiptText.trim()) {
      this.pageMessage.set('Choose a receipt image or paste receipt text before analyzing.');
      this.showToast('warn', 'Receipt needed', 'Add a receipt image or pasted receipt text before analyzing.');
      return;
    }

    this.analyzingReceipt.set(true);
    this.pageMessage.set('');
    this.captureFeedback.set({ tone: 'pending', message: 'Uploading receipt and saving analyzed expense...' });

    this.videoService.analyzeExpense(this.buildAnalyzePayload(), this.selectedFile).subscribe({
      next: (response: AnalyzeExpenseResponse) => {
        this.pageMessage.set(
          response.expense.merchantName
            ? `Receipt analyzed and saved for ${response.expense.merchantName}.`
            : 'Receipt analyzed and saved.'
        );
        this.resetExpenseForm();
        this.captureFeedback.set({ tone: 'success', message: 'Receipt analyzed and saved.' });
        this.showToast('success', 'Receipt saved', 'The analyzed expense was saved.');
        this.loadDashboard({ silentSuccess: true });
        this.analyzingReceipt.set(false);
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'Receipt analysis failed.');
        this.pageMessage.set(message);
        this.analyzingReceipt.set(false);
        this.captureFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Receipt save failed', message);
      }
    });
  }

  importCsv(): void {
    if (!this.selectedCsvFile) {
      this.pageMessage.set('Choose a CSV file before importing.');
      this.showToast('warn', 'CSV needed', 'Choose a statement CSV before importing.');
      return;
    }

    this.importingCsv.set(true);
    this.pageMessage.set('');
    this.csvImportFeedback.set({ tone: 'pending', message: 'Uploading CSV, classifying transactions, and checking for duplicates...' });
    this.csvImportReport.set(null);
    this.selectedImportApprovalKeys.set([]);

    this.videoService.importExpenseCsv(this.settings().modelKey, this.selectedCsvFile).subscribe({
      next: (response) => {
        this.csvImportReport.set(response);
        this.selectedImportApprovalKeys.set(this.reviewApprovalKeys(response));
        this.pageMessage.set(
          `CSV import finished: ${response.addedCount} added, ${response.duplicateCount} already existed, ${response.reviewCount} need review, ${response.failedCount} failed.`
        );
        this.csvImportFeedback.set({
          tone: 'success',
          message: `Import complete using ${response.schemaDisplayName}. Added ${response.addedCount} of ${response.totalRows} rows.`
        });
        this.selectedCsvFile = null;
        this.selectedCsvFileName.set('');
        this.showToast('success', 'CSV import complete', `Added ${response.addedCount}, skipped ${response.duplicateCount}, review ${response.reviewCount}, failed ${response.failedCount}.`);
        this.loadDashboard({ silentSuccess: true });
        this.importingCsv.set(false);
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'CSV import failed.');
        this.pageMessage.set(message);
        this.importingCsv.set(false);
        this.csvImportFeedback.set({ tone: 'error', message });
        this.showToast('error', 'CSV import failed', message);
      }
    });
  }

  openFetchImportDialog(): void {
    this.fetchImportDialogOpen.set(true);
    this.fetchImportFeedback.set(null);
    this.showFetchImportReport.set(false);
  }

  closeFetchImportDialog(): void {
    if (this.importingFetch()) {
      return;
    }

    this.fetchImportDialogOpen.set(false);
  }

  setFetchImportRequestText(value: string): void {
    this.fetchImportRequestText.set(value);
  }

  importFetch(): void {
    const requestText = this.fetchImportRequestText().trim();
    if (!requestText) {
      this.pageMessage.set('Paste the copied fetch request before executing.');
      this.showToast('warn', 'Fetch request needed', 'Paste the copied fetch(...) statement before importing.');
      return;
    }

    this.importingFetch.set(true);
    this.pageMessage.set('');
    this.showFetchImportReport.set(false);
    this.fetchImportFeedback.set({ tone: 'pending', message: 'Running the pasted fetch request, classifying rows, and checking for duplicates...' });
    this.csvImportReport.set(null);
    this.selectedImportApprovalKeys.set([]);

    this.videoService.importExpenseFetch({
      modelKey: this.settings().modelKey,
      requestText
    }).subscribe({
      next: (response) => {
        this.csvImportReport.set(response);
        this.selectedImportApprovalKeys.set(this.reviewApprovalKeys(response));
        this.pageMessage.set(
          `Fetch import finished: ${response.addedCount} added, ${response.duplicateCount} already existed, ${response.reviewCount} need review, ${response.failedCount} failed.`
        );
        this.fetchImportFeedback.set({
          tone: 'success',
          message: `Import complete using ${response.schemaDisplayName}. Added ${response.addedCount} of ${response.totalRows} rows.`
        });
        this.showFetchImportReport.set(true);
        this.showToast('success', 'Fetch import complete', `Added ${response.addedCount}, skipped ${response.duplicateCount}, review ${response.reviewCount}, failed ${response.failedCount}.`);
        this.loadDashboard({ silentSuccess: true });
        this.importingFetch.set(false);
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'Fetch import failed.');
        this.pageMessage.set(message);
        this.importingFetch.set(false);
        this.showFetchImportReport.set(false);
        this.fetchImportFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Fetch import failed', message);
      }
    });
  }

  toggleImportApprovalSelection(item: ExpenseImportApprovalPayload, checked: boolean): void {
    const key = this.importApprovalKey(item);
    const selected = new Set(this.selectedImportApprovalKeys());
    if (checked) {
      selected.add(key);
    } else {
      selected.delete(key);
    }

    this.selectedImportApprovalKeys.set([...selected]);
  }

  isImportApprovalSelected(item: ExpenseImportApprovalPayload | null | undefined): boolean {
    if (!item) {
      return false;
    }

    return this.selectedImportApprovalKeys().includes(this.importApprovalKey(item));
  }

  pendingReviewItemsCount(report: ExpenseCsvImportResponse | null): number {
    return report?.items.filter((item) => item.status === 'review' && item.approvalPayload).length ?? 0;
  }

  selectedReviewItemsCount(report: ExpenseCsvImportResponse | null): number {
    if (!report) {
      return 0;
    }

    return report.items.filter((item) => item.status === 'review' && item.approvalPayload && this.isImportApprovalSelected(item.approvalPayload)).length;
  }

  approveSelectedImportRows(): void {
    const report = this.csvImportReport();
    if (!report) {
      return;
    }

    const rows = report.items
      .filter((item) => item.status === 'review' && item.approvalPayload && this.isImportApprovalSelected(item.approvalPayload))
      .map((item) => item.approvalPayload!) as ExpenseImportApprovalPayload[];

    if (rows.length === 0) {
      this.showToast('warn', 'No review rows selected', 'Choose at least one highlighted row to approve.');
      return;
    }

    const payload: ApproveExpenseImportRowsRequest = {
      schemaKey: report.schemaKey,
      schemaDisplayName: report.schemaDisplayName,
      rows
    };

    this.approvingImportRows.set(true);
    this.csvImportFeedback.set({ tone: 'pending', message: `Approving ${rows.length} review row${rows.length === 1 ? '' : 's'}...` });

    this.videoService.approveExpenseImportRows(payload).subscribe({
      next: (response) => {
        const remainingItems = report.items.filter((item) => item.status !== 'review');
        const nextReport: ExpenseCsvImportResponse = {
          ...report,
          totalRows: remainingItems.length + response.items.length,
          addedCount: remainingItems.filter((item) => item.status === 'added').length + response.addedCount,
          duplicateCount: remainingItems.filter((item) => item.status === 'duplicate').length + response.duplicateCount,
          reviewCount: 0,
          failedCount: remainingItems.filter((item) => item.status === 'failed').length + response.failedCount,
          items: [...remainingItems, ...response.items]
        };

        this.csvImportReport.set(nextReport);
        this.selectedImportApprovalKeys.set([]);
        this.approvingImportRows.set(false);
        this.csvImportFeedback.set({
          tone: 'success',
          message: `Approved ${response.addedCount} row${response.addedCount === 1 ? '' : 's'}.`
        });
        this.pageMessage.set(
          `Review approval finished: ${response.addedCount} added, ${response.duplicateCount} now duplicate, ${response.failedCount} failed.`
        );
        this.showToast('success', 'Review rows approved', `Added ${response.addedCount}, skipped ${response.duplicateCount}, failed ${response.failedCount}.`);
        this.loadDashboard({ silentSuccess: true });
      },
      error: (error) => {
        const message = this.extractErrorMessage(error, 'Import approval failed.');
        this.approvingImportRows.set(false);
        this.csvImportFeedback.set({ tone: 'error', message });
        this.showToast('error', 'Approval failed', message);
      }
    });
  }

  addGroup(): void {
    this.editableGroups.set([
      ...this.editableGroups(),
      {
        id: 0,
        key: '',
        name: '',
        color: '#2563eb',
        isEnabled: true,
        displayOrder: this.editableGroups().length
      }
    ]);
  }

  removeGroup(index: number): void {
    this.editableGroups.set(this.editableGroups().filter((_, currentIndex) => currentIndex !== index));
  }

  updateGroupField<K extends keyof ExpenseGroup>(index: number, field: K, value: ExpenseGroup[K]): void {
    const next = [...this.editableGroups()];
    next[index] = { ...next[index], [field]: value };
    this.editableGroups.set(next);
  }

  updateSettingsField<K extends keyof ExpenseSettings>(field: K, value: ExpenseSettings[K]): void {
    this.settings.set({
      ...this.settings(),
      [field]: value
    });
  }

  saveSettings(): void {
    const validGroups = this.editableGroups()
      .map((group, index) => ({
        ...group,
        displayOrder: index,
        key: (group.key || group.name).trim(),
        name: group.name.trim()
      }))
      .filter((group) => group.name);

    const payload: ExpenseSettingsSaveRequest = {
      settings: this.settings(),
      groups: validGroups
    };

    this.savingSettings.set(true);
    this.pageMessage.set('');

    this.videoService.saveExpenseSettings(payload).subscribe({
      next: (response) => {
        this.pageMessage.set('Expense settings saved.');
        this.expenseGroups.set(response.groups ?? []);
        this.editableGroups.set((response.groups ?? []).map((group) => ({ ...group })));
        this.settings.set(response.settings ?? this.settings());
        this.availableModels.set(response.models ?? []);
        this.summary.set(response.summary ?? this.summary());
        this.syncRouteGroupSelection();
        this.savingSettings.set(false);
        this.showToast('success', 'Settings saved', 'Expense settings were updated.');
      },
      error: (error) => {
        this.pageMessage.set('Failed to save expense settings.');
        this.savingSettings.set(false);
        this.showToast('error', 'Settings save failed', this.extractErrorMessage(error, 'Expense settings could not be saved.'));
      }
    });
  }

  exportCsv(): void {
    const rows = this.expenseEntries();
    if (rows.length === 0) {
      this.pageMessage.set('There are no expenses to export.');
      this.showToast('warn', 'Nothing to export', 'There are no expenses in the current view.');
      return;
    }

    const headers = [
      'Date',
      'Group',
      'Source',
      'Merchant',
      'Location',
      'Description',
      'Payment Method',
      'Currency',
      'Total',
      'Subtotal',
      'Tax',
      'Tip',
      'Notes'
    ];

    const csvRows = [
      headers.join(','),
      ...rows.map((expense) => [
        expense.expenseDate,
        expense.expenseGroupName ?? '',
        expense.sourceType,
        expense.merchantName ?? '',
        expense.location ?? '',
        expense.description ?? '',
        expense.paymentMethod ?? '',
        expense.currencyCode,
        expense.totalAmount,
        expense.subtotalAmount ?? '',
        expense.taxAmount ?? '',
        expense.tipAmount ?? '',
        expense.notes ?? ''
      ].map((value) => this.escapeCsv(String(value))).join(','))
    ];

    const blob = new Blob([csvRows.join('\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `expenses-${this.today()}.csv`;
    link.click();
    URL.revokeObjectURL(url);
    this.showToast('success', 'CSV exported', `Exported ${rows.length} expense${rows.length === 1 ? '' : 's'}.`);
  }

  formatMoney(amount: number | null | undefined, currencyCode?: string | null): string {
    const value = typeof amount === 'number' ? amount : 0;
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currencyCode || this.settings().defaultCurrencyCode || 'USD'
    }).format(value);
  }

  trackByExpenseId(_: number, expense: ExpenseEntry): number {
    return expense.id;
  }

  trackByGroupId(_: number, group: ExpenseGroup): number {
    return group.id || _;
  }

  sourceLabel(sourceType: string): string {
    if (sourceType === 'receipt') {
      return 'Receipt';
    }

    if (sourceType === 'csv-import') {
      return 'CSV Import';
    }

    if (sourceType === 'fetch-import') {
      return 'Fetch Import';
    }

    return 'Manual';
  }

  importStatusLabel(status: string): string {
    if (status === 'review') {
      return 'Needs review';
    }

    if (status === 'duplicate') {
      return 'Duplicate';
    }

    if (status === 'added') {
      return 'Added';
    }

    if (status === 'failed') {
      return 'Failed';
    }

    return status;
  }

  reimbursementBadges(expense: ExpenseEntry): string[] {
    const badges: string[] = [];
    if (expense.isWorkExpense) {
      badges.push('Work');
    }

    if (expense.isReimbursable) {
      badges.push('Reimbursable');
    }

    return badges;
  }

  reportMonthLabel(): string {
    const month = this.reportMonth() || this.currentMonth();
    const [yearString, monthString] = month.split('-');
    const year = Number(yearString);
    const monthIndex = Number(monthString);
    if (!Number.isInteger(year) || !Number.isInteger(monthIndex)) {
      return 'Selected month';
    }

    return new Intl.DateTimeFormat(undefined, {
      month: 'long',
      year: 'numeric'
    }).format(new Date(Date.UTC(year, monthIndex - 1, 1)));
  }

  reportMonthValue(): string {
    return (this.reportMonth() || this.currentMonth()).slice(5, 7);
  }

  reportYear(): string {
    return (this.reportMonth() || this.currentMonth()).slice(0, 4);
  }

  reportYearOptions(): string[] {
    const currentYear = Number(this.currentMonth().slice(0, 4));
    return Array.from({ length: 7 }, (_, index) => String(currentYear - 3 + index));
  }

  reportEntries(): ExpenseEntry[] {
    return [...this.expenseEntries()]
      .filter((expense) => !this.reportExcludeReimbursable() || !expense.isReimbursable)
      .sort((left, right) => left.expenseDate.localeCompare(right.expenseDate));
  }

  reportEntryCount(): number {
    return this.reportEntries().length;
  }

  reportTotalAmount(): number {
    return this.reportEntries().reduce((total, expense) => total + (expense.totalAmount ?? 0), 0);
  }

  reportRangeLabel(): string {
    const start = this.filterDateFrom();
    const end = this.filterDateTo();
    if (!start || !end) {
      return 'Full selected month';
    }

    return start === end ? start : `${start} to ${end}`;
  }

  selectedReportGroupLabel(): string {
    if (this.filterExpenseGroupId() == null) {
      return 'All groups';
    }

    return this.expenseGroups().find((group) => group.id === this.filterExpenseGroupId())?.name || 'Selected group';
  }

  reportCoveredDays(): number {
    const start = this.filterDateFrom();
    const end = this.filterDateTo();
    if (!start || !end) {
      return this.daysInMonth(this.reportMonth());
    }

    const startDate = new Date(`${start}T00:00:00Z`);
    const endDate = new Date(`${end}T00:00:00Z`);
    const diff = Math.round((endDate.getTime() - startDate.getTime()) / 86400000);
    return Math.max(diff + 1, 1);
  }

  reportAveragePerDay(): number {
    const coveredDays = this.reportCoveredDays();
    return coveredDays > 0 ? this.reportTotalAmount() / coveredDays : 0;
  }

  reportProjectedMonthTotal(): number {
    const totalDays = this.daysInMonth(this.reportMonth());
    const coveredDays = this.reportCoveredDays();
    if (coveredDays <= 0) {
      return 0;
    }

    return (this.reportTotalAmount() / coveredDays) * totalDays;
  }

  reportHighestDay(): ReportDailyTotal | null {
    return this.reportDailyTotals().reduce<ReportDailyTotal | null>((highest, current) => {
      if (highest == null || current.totalAmount > highest.totalAmount) {
        return current;
      }

      return highest;
    }, null);
  }

  reportTopGroup(): ReportGroupShare | null {
    return this.reportGroupShares()[0] ?? null;
  }

  reportDailyTotals(): ReportDailyTotal[] {
    const totals = new Map<number, ReportDailyTotal>();
    for (const expense of this.reportEntries()) {
      const day = this.dayOfMonth(expense.expenseDate);
      if (day == null) {
        continue;
      }

      const existing = totals.get(day) ?? {
        day,
        label: `${day}`,
        totalAmount: 0,
        expenseCount: 0
      };

      existing.totalAmount += expense.totalAmount ?? 0;
      existing.expenseCount += 1;
      totals.set(day, existing);
    }

    return [...totals.values()].sort((left, right) => left.day - right.day);
  }

  reportDailyBars(): ReportDailyTotal[] {
    const totalDays = this.daysInMonth(this.reportMonth());
    const byDay = new Map(this.reportDailyTotals().map((item) => [item.day, item]));

    return Array.from({ length: totalDays }, (_, index) => {
      const day = index + 1;
      return byDay.get(day) ?? {
        day,
        label: `${day}`,
        totalAmount: 0,
        expenseCount: 0
      };
    });
  }

  reportDailyChartPoints(): string {
    const bars = this.reportDailyBars();
    if (bars.length === 0) {
      return '';
    }

    const maxAmount = Math.max(...bars.map((item) => item.totalAmount), 1);
    return bars
      .map((item, index) => {
        const x = bars.length === 1 ? 100 : (index / (bars.length - 1)) * 100;
        const y = 100 - ((item.totalAmount / maxAmount) * 100);
        return `${x},${y.toFixed(2)}`;
      })
      .join(' ');
  }

  reportDailyChartAreaPoints(): string {
    const line = this.reportDailyChartPoints();
    return line ? `0,100 ${line} 100,100` : '';
  }

  reportMaxDailySpend(): number {
    return Math.max(...this.reportDailyBars().map((item) => item.totalAmount), 0);
  }

  reportGroupShares(): ReportGroupShare[] {
    const totals = new Map<string, ReportGroupShare>();
    const totalAmount = this.reportTotalAmount();

    for (const expense of this.reportEntries()) {
      const name = expense.expenseGroupName || 'Unclassified';
      const color = expense.expenseGroupColor || '#64748b';
      const key = `${name}|${color}`;
      const existing = totals.get(key) ?? {
        name,
        color,
        totalAmount: 0,
        expenseCount: 0,
        percent: 0
      };

      existing.totalAmount += expense.totalAmount ?? 0;
      existing.expenseCount += 1;
      totals.set(key, existing);
    }

    return [...totals.values()]
      .sort((left, right) => right.totalAmount - left.totalAmount)
      .map((item) => ({
        ...item,
        percent: totalAmount > 0 ? (item.totalAmount / totalAmount) * 100 : 0
      }));
  }

  openReportGroup(group: ReportGroupShare): void {
    this.selectedReportGroup.set({
      name: group.name,
      color: group.color
    });
  }

  closeReportGroup(): void {
    this.selectedReportGroup.set(null);
  }

  reportGroupExpenses(): ExpenseEntry[] {
    const selectedGroup = this.selectedReportGroup();
    if (!selectedGroup) {
      return [];
    }

    return this.reportEntries()
      .filter((expense) => (expense.expenseGroupName || 'Unclassified') === selectedGroup.name)
      .sort((left, right) => {
        const dateCompare = right.expenseDate.localeCompare(left.expenseDate);
        if (dateCompare !== 0) {
          return dateCompare;
        }

        return right.id - left.id;
      });
  }

  reportGroupTotal(): number {
    return this.reportGroupExpenses().reduce((total, expense) => total + (expense.totalAmount ?? 0), 0);
  }

  reportWeekdayTotals(): ReportWeekdayTotal[] {
    const order = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    const totals = order.map((label) => ({ label, totalAmount: 0, expenseCount: 0 }));

    for (const expense of this.reportEntries()) {
      const date = new Date(`${expense.expenseDate}T00:00:00Z`);
      const bucket = totals[date.getUTCDay()];
      bucket.totalAmount += expense.totalAmount ?? 0;
      bucket.expenseCount += 1;
    }

    return totals;
  }

  reportMaxWeekdaySpend(): number {
    return Math.max(...this.reportWeekdayTotals().map((item) => item.totalAmount), 0);
  }

  reportRecurringMerchants(): ReportMerchantInsight[] {
    const totals = new Map<string, ReportMerchantInsight>();
    for (const expense of this.reportEntries()) {
      const merchantName = (expense.merchantName || '').trim();
      if (!merchantName) {
        continue;
      }

      const normalized = merchantName.toLowerCase();
      const existing = totals.get(normalized) ?? {
        merchantName,
        totalAmount: 0,
        expenseCount: 0
      };

      existing.totalAmount += expense.totalAmount ?? 0;
      existing.expenseCount += 1;
      totals.set(normalized, existing);
    }

    return [...totals.values()]
      .filter((item) => item.expenseCount > 1)
      .sort((left, right) => {
        if (right.expenseCount !== left.expenseCount) {
          return right.expenseCount - left.expenseCount;
        }

        return right.totalAmount - left.totalAmount;
      })
      .slice(0, 5);
  }

  openExpenseDetail(expenseId: number): void {
    this.loadingExpenseDetail.set(true);
    this.detailFeedback.set({ tone: 'pending', message: 'Loading expense details...' });
    this.videoService.getExpense(expenseId).subscribe({
      next: (detail) => {
        this.selectedExpenseDetail.set(detail);
        this.editingExpenseForm.set({
          id: detail.expense.id,
          expenseGroupId: detail.expense.expenseGroupId ?? null,
          expenseDate: detail.expense.expenseDate,
          totalAmount: String(detail.expense.totalAmount ?? ''),
          subtotalAmount: detail.expense.subtotalAmount != null ? String(detail.expense.subtotalAmount) : '',
          taxAmount: detail.expense.taxAmount != null ? String(detail.expense.taxAmount) : '',
          tipAmount: detail.expense.tipAmount != null ? String(detail.expense.tipAmount) : '',
          currencyCode: detail.expense.currencyCode || this.settings().defaultCurrencyCode,
          merchantName: detail.expense.merchantName ?? '',
          location: detail.expense.location ?? '',
          paymentMethod: detail.expense.paymentMethod ?? '',
          description: detail.expense.description ?? '',
          notes: detail.expense.notes ?? '',
          receiptText: detail.expense.receiptText ?? '',
          isWorkExpense: detail.expense.isWorkExpense ?? false,
          isReimbursable: detail.expense.isReimbursable ?? false
        });
        this.loadingExpenseDetail.set(false);
        this.detailFeedback.set(null);
        this.showToast('info', 'Expense loaded', 'Expense details are ready to edit.');
      },
      error: () => {
        this.pageMessage.set('Failed to load expense details.');
        this.loadingExpenseDetail.set(false);
        this.detailFeedback.set({ tone: 'error', message: 'Expense details could not be loaded.' });
        this.showToast('error', 'Load failed', 'Expense details could not be loaded.');
      }
    });
  }

  closeExpenseDetail(): void {
    this.selectedExpenseDetail.set(null);
    this.detailFeedback.set(null);
  }

  updateExpenseDetailField<K extends keyof ReturnType<ExpensesComponent['editingExpenseForm']>>(field: K, value: ReturnType<ExpensesComponent['editingExpenseForm']>[K]): void {
    this.editingExpenseForm.set({
      ...this.editingExpenseForm(),
      [field]: value
    });
  }

  saveExpenseDetail(): void {
    const form = this.editingExpenseForm();
    const totalAmount = this.parseNumber(form.totalAmount);
    if (totalAmount == null) {
      this.pageMessage.set('A total amount is required before saving edits.');
      this.showToast('warn', 'Missing total', 'A total amount is required before saving edits.');
      return;
    }

    const payload: UpdateExpenseRequest = {
      expenseGroupId: form.expenseGroupId,
      expenseDate: form.expenseDate,
      totalAmount,
      subtotalAmount: this.parseNumber(form.subtotalAmount) ?? undefined,
      taxAmount: this.parseNumber(form.taxAmount) ?? undefined,
      tipAmount: this.parseNumber(form.tipAmount) ?? undefined,
      currencyCode: form.currencyCode,
      merchantName: form.merchantName || undefined,
      location: form.location || undefined,
      paymentMethod: form.paymentMethod || undefined,
      description: form.description || undefined,
      notes: form.notes || undefined,
      receiptText: form.receiptText || undefined,
      isWorkExpense: form.isWorkExpense,
      isReimbursable: form.isReimbursable
    };

    this.savingExpenseDetail.set(true);
    this.detailFeedback.set({ tone: 'pending', message: 'Saving expense changes...' });
    this.videoService.updateExpense(form.id, payload).subscribe({
      next: () => {
        this.pageMessage.set('Expense changes saved.');
        this.closeExpenseDetail();
        this.historyFeedback.set({ tone: 'success', message: 'Expense changes saved.' });
        this.showToast('success', 'Expense updated', 'Your changes were saved.');
        this.loadDashboard({ silentSuccess: true });
        this.savingExpenseDetail.set(false);
      },
      error: (error) => {
        this.pageMessage.set('Failed to save expense changes.');
        this.savingExpenseDetail.set(false);
        this.detailFeedback.set({ tone: 'error', message: 'Expense changes could not be saved.' });
        this.showToast('error', 'Save failed', this.extractErrorMessage(error, 'Expense changes could not be saved.'));
      }
    });
  }

  private buildManualPayload(): CreateManualExpenseRequest | null {
    const form = this.expenseForm();
    if (form.expenseGroupId == null) {
      return null;
    }

    const totalAmount = this.parseNumber(form.totalAmount);

    if (totalAmount == null) {
      return null;
    }

    return {
      expenseGroupId: form.expenseGroupId,
      expenseDate: form.expenseDate || undefined,
      totalAmount,
      subtotalAmount: this.parseNumber(form.subtotalAmount) ?? undefined,
      taxAmount: this.parseNumber(form.taxAmount) ?? undefined,
      tipAmount: this.parseNumber(form.tipAmount) ?? undefined,
      currencyCode: form.currencyCode || this.settings().defaultCurrencyCode,
      merchantName: form.merchantName || undefined,
      location: form.location || undefined,
      paymentMethod: form.paymentMethod || undefined,
      description: form.description || undefined,
      notes: form.notes || undefined
    };
  }

  private buildAnalyzePayload(): Record<string, string> {
    const form = this.expenseForm();
    const payload: Record<string, string> = {};
    this.appendPayloadValue(payload, 'modelKey', this.settings().modelKey);
    this.appendPayloadValue(payload, 'expenseGroupId', form.expenseGroupId);
    this.appendPayloadValue(payload, 'expenseDate', form.expenseDate);
    this.appendPayloadValue(payload, 'totalAmount', this.parseNumber(form.totalAmount));
    this.appendPayloadValue(payload, 'subtotalAmount', this.parseNumber(form.subtotalAmount));
    this.appendPayloadValue(payload, 'taxAmount', this.parseNumber(form.taxAmount));
    this.appendPayloadValue(payload, 'tipAmount', this.parseNumber(form.tipAmount));
    this.appendPayloadValue(payload, 'currencyCode', form.currencyCode);
    this.appendPayloadValue(payload, 'merchantName', form.merchantName);
    this.appendPayloadValue(payload, 'location', form.location);
    this.appendPayloadValue(payload, 'paymentMethod', form.paymentMethod);
    this.appendPayloadValue(payload, 'description', form.description);
    this.appendPayloadValue(payload, 'notes', form.notes);
    this.appendPayloadValue(payload, 'receiptText', form.receiptText);
    return payload;
  }

  private applyPreviewToForm(preview: ExpensePreviewResponse): void {
    this.expenseForm.set({
      ...this.expenseForm(),
      expenseGroupId: preview.expenseGroupId ?? this.expenseForm().expenseGroupId,
      expenseDate: preview.expenseDate || this.expenseForm().expenseDate,
      totalAmount: preview.totalAmount != null ? String(preview.totalAmount) : this.expenseForm().totalAmount,
      subtotalAmount: preview.subtotalAmount != null ? String(preview.subtotalAmount) : this.expenseForm().subtotalAmount,
      taxAmount: preview.taxAmount != null ? String(preview.taxAmount) : this.expenseForm().taxAmount,
      tipAmount: preview.tipAmount != null ? String(preview.tipAmount) : this.expenseForm().tipAmount,
      currencyCode: preview.currencyCode || this.expenseForm().currencyCode,
      merchantName: preview.merchantName ?? this.expenseForm().merchantName,
      location: preview.location ?? this.expenseForm().location,
      paymentMethod: preview.paymentMethod ?? this.expenseForm().paymentMethod,
      description: preview.description ?? this.expenseForm().description,
      notes: preview.notes ?? this.expenseForm().notes,
      receiptText: preview.receiptText ?? this.expenseForm().receiptText
    });
  }

  private appendPayloadValue(payload: Record<string, string>, key: string, value: unknown): void {
    if (value == null) {
      return;
    }

    if (typeof value === 'string' && !value.trim()) {
      return;
    }

    payload[key] = String(value);
  }

  private parseNumber(value: string | number | null | undefined): number | null {
    if (value == null) {
      return null;
    }

    if (typeof value === 'number') {
      return Number.isFinite(value) ? value : null;
    }

    if (!value.trim()) {
      return null;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private extractErrorMessage(error: any, fallback: string): string {
    if (typeof error?.error === 'string' && error.error.trim()) {
      return error.error;
    }

    if (typeof error?.error?.title === 'string' && error.error.title.trim()) {
      return error.error.title;
    }

    if (typeof error?.message === 'string' && error.message.trim()) {
      return error.message;
    }

    return fallback;
  }

  private showToast(severity: ToastSeverity, summary: string, detail: string): void {
    this.messageService.add({
      severity,
      summary,
      detail,
      life: severity === 'error' ? 5000 : 3000
    });
  }

  private reviewApprovalKeys(report: ExpenseCsvImportResponse): string[] {
    return report.items
      .filter((item) => item.status === 'review' && item.approvalPayload)
      .map((item) => this.importApprovalKey(item.approvalPayload!));
  }

  private importApprovalKey(item: ExpenseImportApprovalPayload): string {
    return `${item.schemaKey}|${item.rowNumber}|${item.sourceReference ?? item.expenseDate}|${item.totalAmount}`;
  }

  private resetExpenseForm(): void {
    this.selectedFile = null;
    this.selectedFileName.set('');
    this.expenseForm.set({
      expenseGroupId: null,
      expenseDate: this.today(),
      totalAmount: '',
      subtotalAmount: '',
      taxAmount: '',
      tipAmount: '',
      currencyCode: this.settings().defaultCurrencyCode || 'USD',
      merchantName: '',
      location: '',
      paymentMethod: '',
      description: '',
      notes: '',
      receiptText: ''
    });
  }

  private syncCurrencyDefault(): void {
    if (!this.expenseForm().currencyCode) {
      this.updateForm('currencyCode', this.settings().defaultCurrencyCode || 'USD');
    }
  }

  private syncRouteGroupSelection(): void {
    const group = this.route.snapshot.queryParamMap.get('group');
    if (!group || group === 'all') {
      this.filterExpenseGroupId.set(null);
      return;
    }

    const matchedGroup = this.expenseGroups().find((item) => item.key === group || String(item.id) === group);
    this.filterExpenseGroupId.set(matchedGroup?.id ?? null);
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private currentMonth(): string {
    return this.today().slice(0, 7);
  }

  private initializeReportFilters(): void {
    const activeMonth = this.filterDateFrom().slice(0, 7) || this.filterDateTo().slice(0, 7) || this.currentMonth();
    this.reportMonth.set(activeMonth);
    this.reportExcludeReimbursable.set(true);

    const monthDayFrom = this.filterDateFrom() && this.filterDateFrom().startsWith(activeMonth)
      ? String(this.dayOfMonth(this.filterDateFrom()) ?? '')
      : '';
    const monthDayTo = this.filterDateTo() && this.filterDateTo().startsWith(activeMonth)
      ? String(this.dayOfMonth(this.filterDateTo()) ?? '')
      : '';

    this.reportDayFrom.set(monthDayFrom === '1' ? '' : monthDayFrom);
    const monthEnd = String(this.daysInMonth(activeMonth));
    this.reportDayTo.set(monthDayTo === monthEnd ? '' : monthDayTo);
  }

  private daysInMonth(month: string): number {
    const [yearString, monthString] = month.split('-');
    const year = Number(yearString);
    const monthIndex = Number(monthString);
    if (!Number.isInteger(year) || !Number.isInteger(monthIndex) || monthIndex < 1 || monthIndex > 12) {
      return 31;
    }

    return new Date(Date.UTC(year, monthIndex, 0)).getUTCDate();
  }

  private buildMonthDate(month: string, day: number): string {
    return `${month}-${String(day).padStart(2, '0')}`;
  }

  private dayOfMonth(dateValue: string | null | undefined): number | null {
    if (!dateValue) {
      return null;
    }

    const day = Number(dateValue.slice(8, 10));
    return Number.isInteger(day) ? day : null;
  }

  private escapeCsv(value: string): string {
    const escaped = value.replace(/"/g, '""');
    return /[",\n]/.test(escaped) ? `"${escaped}"` : escaped;
  }
}
