import { CurrencyPipe, DatePipe, NgFor } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatAutocomplete, MatAutocompleteTrigger, MatOption } from '@angular/material/autocomplete';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSelect } from '@angular/material/select';
import { MatTable, MatColumnDef, MatHeaderCell, MatHeaderCellDef, MatCell, MatCellDef, MatHeaderRow, MatHeaderRowDef, MatRow, MatRowDef } from '@angular/material/table';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { ClaimListFilters, ClaimListItem, ClaimStatus, User } from '../../core/models/api.models';
import { ClaimsApiService } from '../../core/services/claims-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { UsersApiService } from '../../core/services/users-api.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { LoadingIndicatorComponent } from '../../shared/loading/loading-indicator.component';
import { StatusBadgeComponent } from '../../shared/status-badge/status-badge.component';

const claimStatuses: ClaimStatus[] = ['Draft', 'Open', 'UnderInvestigation', 'PendingPayment', 'Closed', 'Reopened', 'Withdrawn'];

@Component({ selector: 'app-claims-list', standalone: true, imports: [ReactiveFormsModule, RouterLink, CurrencyPipe, DatePipe, NgFor, MatButton, MatFormField, MatLabel, MatInput, MatSelect, MatOption, MatAutocomplete, MatAutocompleteTrigger, MatTable, MatColumnDef, MatHeaderCell, MatHeaderCellDef, MatCell, MatCellDef, MatHeaderRow, MatHeaderRowDef, MatRow, MatRowDef, EmptyStateComponent, LoadingIndicatorComponent, StatusBadgeComponent], templateUrl: './claims-list.component.html', styleUrl: './claims-list.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class ClaimsListComponent {
  private readonly claimsApi = inject(ClaimsApiService); private readonly referenceApi = inject(ReferenceApiService); private readonly usersApi = inject(UsersApiService); private readonly route = inject(ActivatedRoute); private readonly router = inject(Router); private readonly destroyRef = inject(DestroyRef); private readonly fb = inject(FormBuilder);
  readonly statuses = claimStatuses; readonly causes = signal<{ code: string; name: string }[]>([]); readonly users = signal<User[]>([]); readonly rows = signal<ClaimListItem[]>([]); readonly totalCount = signal(0); readonly loading = signal(true); readonly displayedColumns = ['claimNumber', 'policyNumber', 'clientName', 'lossDate', 'causeOfLoss', 'status', 'totalReserves'];
  readonly filters = this.fb.nonNullable.group({ status: this.fb.nonNullable.control<ClaimStatus[]>([]), dateFrom: '', dateTo: '', assignedHandler: this.fb.nonNullable.control<string | User>(''), causeOfLossCode: '', search: '' });
  readonly loadError = signal(false);
  private readonly handlerSearch = signal('');
  private selectedHandlerId?: string;
  readonly filteredUsers = computed(() => this.users().filter(user => user.name.toLowerCase().includes(this.handlerSearch().toLowerCase())));
  currentFilters: ClaimListFilters = { page: 1, pageSize: 20 };
  constructor() {
    this.referenceApi.causes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((causes) => this.causes.set(causes));
    this.usersApi.list().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(users => {
      this.users.set(users);
      const selected = users.find(user => user.id === this.selectedHandlerId);
      if (selected) { this.filters.controls.assignedHandler.setValue(selected.name, { emitEvent: false }); this.handlerSearch.set(selected.name); }
    });
    this.filters.controls.assignedHandler.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      this.handlerSearch.set(typeof value === 'string' ? value : value.name);
      this.selectedHandlerId = typeof value === 'string' ? undefined : value.id;
    });
    this.route.queryParamMap.pipe(switchMap(params => {
      this.currentFilters = this.filtersFromParams(params);
      this.patchForm(this.currentFilters);
      this.loading.set(true); this.loadError.set(false); this.rows.set([]); this.totalCount.set(0);
      return this.claimsApi.list({ ...this.currentFilters,
        dateFrom: this.utcDayBound(this.currentFilters.dateFrom, false),
        dateTo: this.utcDayBound(this.currentFilters.dateTo, true)
      }).pipe(catchError(() => { this.rows.set([]); this.totalCount.set(0); this.loadError.set(true); this.loading.set(false); return EMPTY; }));
    }), takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      this.rows.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false);
    });
    this.filters.valueChanges.pipe(debounceTime(300), distinctUntilChanged((left, right) => JSON.stringify(left) === JSON.stringify(right)), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.updateQuery({ page: 1 }));
  }
  selectHandler(user: User): void { this.selectedHandlerId = user.id; this.handlerSearch.set(user.name); this.filters.controls.assignedHandler.setValue(user.name, { emitEvent: false }); this.updateQuery({ page: 1 }); }
  handlerId(): string | undefined { return this.selectedHandlerId; }
  displayUser(user: User | string | null): string { return typeof user === 'string' ? user : user?.name ?? ''; }
  resetFilters(): void { this.filters.reset({ status: [], dateFrom: '', dateTo: '', assignedHandler: '', causeOfLossCode: '', search: '' }); }
  changePage(page: number): void { this.updateQuery({ page }); } changePageSize(pageSize: number): void { this.updateQuery({ page: 1, pageSize }); } openClaim(row: ClaimListItem): void { void this.router.navigate(['/claims', row.id]); }
  private utcDayBound(value: string | undefined, end: boolean): string | undefined {
    if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
    return value + (end ? 'T23:59:59.9999999Z' : 'T00:00:00.000Z');
  }
  private updateQuery(overrides: Partial<ClaimListFilters>): void { const value = this.filters.getRawValue(); const query: Params = { status: value.status.length ? value.status : null, dateFrom: value.dateFrom || null, dateTo: value.dateTo || null, assignedHandlerId: this.handlerId() ?? null, causeOfLossCode: value.causeOfLossCode || null, search: value.search || null, page: overrides.page ?? this.currentFilters.page ?? 1, pageSize: overrides.pageSize ?? this.currentFilters.pageSize ?? 20 }; void this.router.navigate([], { relativeTo: this.route, queryParams: query }); }
  private filtersFromParams(params: { get(name: string): string | null; getAll(name: string): string[] }): ClaimListFilters { const status = params.getAll('status').filter((value): value is ClaimStatus => claimStatuses.includes(value as ClaimStatus)); return { page: this.positiveNumber(params.get('page'), 1), pageSize: this.positiveNumber(params.get('pageSize'), 20), status, dateFrom: params.get('dateFrom') ?? undefined, dateTo: params.get('dateTo') ?? undefined, assignedHandlerId: params.get('assignedHandlerId') ?? undefined, causeOfLossCode: params.get('causeOfLossCode') ?? undefined, search: params.get('search') ?? undefined }; }
  private patchForm(filters: ClaimListFilters): void { this.selectedHandlerId = filters.assignedHandlerId; const name = this.users().find(user => user.id === filters.assignedHandlerId)?.name ?? ''; this.handlerSearch.set(name); this.filters.patchValue({ status: filters.status ?? [], dateFrom: filters.dateFrom ?? '', dateTo: filters.dateTo ?? '', assignedHandler: name, causeOfLossCode: filters.causeOfLossCode ?? '', search: filters.search ?? '' }, { emitEvent: false }); }
  private positiveNumber(value: string | null, fallback: number): number { const number = Number(value); return Number.isInteger(number) && number > 0 ? number : fallback; }
}
