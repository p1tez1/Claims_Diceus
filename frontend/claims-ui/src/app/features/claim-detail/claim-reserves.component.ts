import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, Input, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatOption } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltip } from '@angular/material/tooltip';
import { take, timer } from 'rxjs';
import { ApiError, ClaimDetail, ReserveComponent, ReserveComponentType, ReserveHistory, ReserveTransactionType } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { ReservesApiService } from '../../core/services/reserves-api.service';
import { authorityIndicator } from '../../shared/authority/authority-indicator.pipe';
import { ReserveRejectDialogComponent } from './reserve-reject-dialog.component';
import { nonBlank } from '../fnol-intake/fnol-forms';

const COMPONENTS: ReserveComponentType[] = ['Indemnity', 'Expense', 'ALAE', 'SubrogationRecoverable'];

@Component({ selector: 'app-claim-reserves', standalone: true,
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, MatButton, MatOption, MatError, MatFormField, MatLabel, MatInput, MatProgressSpinner, MatSelect, MatTooltip],
  template: `
    <div class="heading"><div><h2>Reserves</h2><p>Current balances and transaction history</p></div><div class="actions"><button mat-button type="button" [disabled]="loading()" (click)="refresh()">Refresh</button><button mat-flat-button color="primary" type="button" [disabled]="!canSubmit() || busy() !== null" [matTooltip]="!claim.policyId ? 'Link a policy before adding reserves.' : ''" (click)="panelOpen.set(true)">Add Reserve</button></div></div>
    @if (!claim.policyId) { <p class="block">A policy must be linked before a reserve can be submitted.</p> }
    @if (warning() || overrideRequired()) { <div class="warning"><span>{{ warning() || 'Manager override is required before approval above the aggregate cap.' }}</span>@if (auth.isManager() && !claim.managerOverride && overrideRequired()) { <button mat-stroked-button type="button" [disabled]="busy() !== null" (click)="setOverride()">@if (busy() === 'override') { <mat-spinner diameter="18" /> } Set override</button> }</div> }
    @if (loading()) { <div class="loading"><mat-spinner diameter="30" /><span>Loading reserves</span></div> }
    @else {
      <div class="cards">@for (component of components; track component) { <article><strong>{{ component }}</strong><span>Current Balance</span><b>{{ balance(component) | currency }}</b><span>Pending Amount</span><b [class.pending]="pending(component) !== 0">{{ pending(component) | currency }}</b></article> }</div>
      <div class="table-wrap"><table><thead><tr><th>Date</th><th>Type</th><th>Component</th><th>Amount</th><th>Approval</th><th>GL posting</th><th>Submitted By</th><th>Approved By</th><th>Actions</th></tr></thead><tbody>
        @for (row of history(); track row.id) { <tr [id]="'reserve-'+row.id"><td>{{ row.createdAt | date:'medium' }}</td><td>{{ row.transactionType }}</td><td>{{ row.component }}</td><td [class.increase]="row.amount > 0" [class.decrease]="row.amount < 0">{{ row.amount | currency }}</td><td><span class="badge" [class.pending]="row.approvalStatus === 'PendingApproval'">{{ row.approvalStatus }}</span></td><td><span class="badge" [class.failed]="row.postingStatus === 'Failed'">{{ row.postingStatus }}</span></td><td>{{ row.submittedByName || name(row.submittedByUserId) }}</td><td>{{ row.approvedByName || name(row.approvedByUserId) }}</td><td class="row-actions">
          @if (row.approvalStatus === 'PendingApproval' && auth.isSupervisorOrAbove()) { <span [matTooltip]="auth.canReviewReserve(row) ? '' : auth.reserveReviewBlockReason(row)"><button mat-button type="button" [disabled]="!auth.canReviewReserve(row) || busy() !== null" (click)="approve(row)">@if (busy() === row.id + ':approve') { <mat-spinner diameter="16" /> } Approve</button></span><span [matTooltip]="auth.canReviewReserve(row) ? '' : auth.reserveReviewBlockReason(row)"><button mat-button color="warn" type="button" [disabled]="!auth.canReviewReserve(row) || busy() !== null" (click)="reject(row)">@if (busy() === row.id + ':reject') { <mat-spinner diameter="16" /> } Reject</button></span> }
          @if (auth.canRetractReserve(row)) { <button mat-button type="button" [disabled]="busy() !== null" (click)="retract(row)">@if (busy() === row.id + ':retract') { <mat-spinner diameter="16" /> } Retract</button> }
          @if (auth.canRetryPosting(row)) { <button mat-button type="button" [disabled]="busy() !== null" (click)="retry(row)">@if (busy() === row.id + ':retry') { <mat-spinner diameter="16" /> } Retry</button> }
        </td></tr> } @empty { <tr><td colspan="9" class="empty">No reserve transactions yet.</td></tr> }
      </tbody></table></div>
    }
    @if (panelOpen()) { <div class="backdrop" (click)="closePanel()"></div><aside class="drawer" aria-label="Add reserve"><div class="drawer-title"><h2>Add Reserve</h2><button mat-button type="button" [disabled]="busy() !== null" (click)="closePanel()">Close</button></div><form [formGroup]="form" (ngSubmit)="submit()">
      <mat-form-field appearance="outline"><mat-label>Component</mat-label><mat-select formControlName="component">@for (component of components; track component) { <mat-option [value]="component">{{ component }}</mat-option> }</mat-select>@if (form.controls.component.invalid) { <mat-error>Select a component.</mat-error> }</mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Type</mat-label><mat-select formControlName="transactionType"><mat-option value="Add">Add</mat-option><mat-option value="Adjust">Adjust</mat-option><mat-option value="Reverse">Reverse</mat-option></mat-select>@if (form.controls.transactionType.invalid) { <mat-error>Select a transaction type.</mat-error> }</mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Amount</mat-label><input matInput type="number" step="0.01" formControlName="amount">@if (form.controls.amount.invalid) { <mat-error>Enter a non-zero amount with the correct sign for this transaction type.</mat-error> }</mat-form-field>
      <p class="hint">Use a positive magnitude for Reverse. Adjust accepts a signed change.</p>
      <mat-form-field appearance="outline"><mat-label>Reason</mat-label><textarea matInput rows="3" maxlength="500" formControlName="changeReason"></textarea>@if (form.controls.changeReason.invalid) { <mat-error>A reason of up to 500 characters is required.</mat-error> }</mat-form-field>
      @if (rejectedForComponent().length && form.controls.transactionType.value !== 'Reverse') { <mat-form-field appearance="outline"><mat-label>Resubmit rejected transaction (optional)</mat-label><mat-select formControlName="resubmissionOfId"><mat-option value="">None</mat-option>@for (row of rejectedForComponent(); track row.id) { <mat-option [value]="row.id">{{ row.component }} · {{ row.amount | currency }} · {{ row.createdAt | date:'shortDate' }}</mat-option> }</mat-select></mat-form-field> }
      @if (form.controls.amount.value !== null && form.controls.amount.value !== 0) { <p class="authority">{{ authorityIndicator(form.controls.amount.value).label }}</p> }
      @if (formError()) { <p class="block">{{ formError() }}</p> }
      <button mat-flat-button color="primary" type="submit" [disabled]="!canSubmit() || form.invalid || busy() !== null">@if (busy() === 'submit') { <mat-spinner diameter="18" /> } Submit reserve</button>
    </form></aside> }
  `,
  styles: [`:host{display:block}.heading,.actions,.drawer-title,.warning{display:flex;align-items:center;justify-content:space-between;gap:1rem}.heading h2{margin:0}.heading p{margin:.2rem 0 1rem;color:var(--claims-text-muted)}.actions{justify-content:flex-end}.cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:.75rem;margin:1rem 0}.cards article{display:grid;gap:.3rem;padding:1rem;border:1px solid var(--claims-border);border-radius:.7rem}.cards span{font-size:.75rem;color:var(--claims-text-muted)}.cards b{font-size:1.15rem}.pending{color:#a96500!important}.warning,.block{padding:.8rem 1rem;background:#fff7ed;color:#92400e;border-radius:.6rem}.warning{margin:1rem 0}.loading{display:flex;gap:1rem;align-items:center;min-height:180px}.table-wrap{overflow:auto}table{border-collapse:collapse;width:100%;min-width:1000px;text-align:left}th,td{padding:.7rem;border-bottom:1px solid var(--claims-border);font-size:.84rem}th{color:var(--claims-text-muted)}.increase{color:#166534;font-weight:700}.decrease,.failed{color:#b91c1c!important;font-weight:700}.badge{display:inline-block;border-radius:999px;background:#eef2f5;padding:.2rem .55rem;font-size:.75rem}.row-actions{white-space:nowrap}.row-actions mat-spinner,.warning mat-spinner,.drawer mat-spinner{display:inline-block;vertical-align:middle;margin-right:.3rem}.empty{text-align:center;color:var(--claims-text-muted)}.backdrop{position:fixed;inset:0;background:#0f172a66;z-index:100}.drawer{position:fixed;z-index:101;right:0;top:0;height:100vh;width:min(420px,100vw);padding:1.5rem;background:white;box-shadow:-12px 0 30px #0f172a33;overflow:auto}.drawer form{display:grid;gap:.65rem;margin-top:1rem}.drawer mat-form-field{width:100%}.hint{font-size:.8rem;color:var(--claims-text-muted);margin:-.5rem 0 .5rem}@media(max-width:900px){.cards{grid-template-columns:repeat(2,1fr)}}@media(max-width:600px){.cards{grid-template-columns:1fr}.heading{align-items:flex-start;flex-direction:column}}`],
  changeDetection: ChangeDetectionStrategy.OnPush })
export class ClaimReservesComponent {
  @Input({ required: true }) claim!: ClaimDetail;
  @Input() focusId: string | null = null;
  private readonly api = inject(ReservesApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);
  readonly components = COMPONENTS;
  readonly authorityIndicator = authorityIndicator;
  readonly reserves = signal<ReserveComponent[]>([]);
  readonly loading = signal(true);
  readonly busy = signal<string | null>(null);
  readonly panelOpen = signal(false);
  readonly warning = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly history = computed(() => [...this.reserves()].sort((a, b) => COMPONENTS.indexOf(a.component) - COMPONENTS.indexOf(b.component)).flatMap(component => [...component.history].sort((a, b) => a.changeSequence - b.changeSequence).map(row => ({ ...row, component: component.component }))));
  readonly rejected = computed(() => this.history().filter(row => row.approvalStatus === 'Rejected'));
  readonly rejectedForComponent = () => this.rejected().filter(row => row.component === this.form.controls.component.value);
  readonly form = new FormGroup({
    component: new FormControl<ReserveComponentType>('Indemnity', { nonNullable: true, validators: Validators.required }),
    transactionType: new FormControl<ReserveTransactionType>('Add', { nonNullable: true, validators: Validators.required }),
    amount: new FormControl<number | null>(null, [Validators.required, control => {
      const amount = control.value as number | null;
      if (amount === null) return null;
      const type = control.parent?.get('transactionType')?.value ?? 'Add';
      const component = control.parent?.get('component')?.value ?? 'Indemnity';
      return !Number.isFinite(amount) || amount === 0 || (type === 'Add' && (component === 'SubrogationRecoverable' ? amount >= 0 : amount <= 0)) || (type === 'Reverse' && amount <= 0) ? { amountSign: true } : null;
    }]),
    changeReason: new FormControl('', { nonNullable: true, validators: [nonBlank, Validators.maxLength(500)] }),
    resubmissionOfId: new FormControl('', { nonNullable: true })
  });

  ngOnInit(): void {
    this.form.controls.component.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.form.controls.amount.updateValueAndValidity({ emitEvent: false }));
    this.form.controls.transactionType.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.form.controls.amount.updateValueAndValidity({ emitEvent: false }));
    this.refresh();
  }
  canSubmit(): boolean { return !!this.claim.policyId && !['Closed', 'Withdrawn'].includes(this.claim.status); }
  overrideRequired(): boolean {
    if (this.claim.managerOverride) return false;
    const total = this.reserves().reduce((sum, component) => sum + component.currentAmount, 0);
    return this.history().some(row => row.approvalStatus === 'PendingApproval' && total + row.amount > 10_000_000);
  }
  balance(component: ReserveComponentType): number { return this.reserves().find(row => row.component === component)?.currentAmount ?? 0; }
  pending(component: ReserveComponentType): number { return this.reserves().find(row => row.component === component)?.pendingAmount ?? 0; }
  name(userId?: string): string { return userId ? (userId === this.auth.currentUser()?.id ? this.auth.currentUser()?.name ?? userId : userId) : '—'; }
  refresh(): void { this.loading.set(true); this.api.get(this.claim.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => { this.reserves.set(rows); this.loading.set(false); if (this.focusId) setTimeout(() => document.getElementById(this.focusId!)?.scrollIntoView({ block: 'center' }), 50); }, error: () => this.loading.set(false) }); }
  closePanel(): void { if (!this.busy()) this.panelOpen.set(false); }
  submit(): void {
    if (this.form.invalid || !this.canSubmit() || this.busy()) return;
    const value = this.form.getRawValue(); const amount = value.amount!;
    if (amount === 0 || (value.transactionType === 'Add' && (value.component === 'SubrogationRecoverable' ? amount >= 0 : amount <= 0)) || (value.transactionType === 'Reverse' && amount <= 0)) { this.formError.set('Enter an amount with the correct sign for this transaction type.'); return; }
    if (value.resubmissionOfId && (value.transactionType === 'Reverse' || !this.rejectedForComponent().some(row => row.id === value.resubmissionOfId))) { this.formError.set('Choose a rejected transaction for this component and use Add or Adjust.'); return; }
    this.formError.set(null); this.busy.set('submit');
    this.api.submit(this.claim.id, { component: value.component, amount, changeReason: value.changeReason.trim(), transactionType: value.transactionType, ...(value.resubmissionOfId ? { resubmissionOfId: value.resubmissionOfId } : {}) }, crypto.randomUUID())
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.busy.set(null); this.panelOpen.set(false); this.form.reset({ component: 'Indemnity', transactionType: 'Add', amount: null, changeReason: '', resubmissionOfId: '' }); this.warning.set(result.warnings.find(item => item.code === 'BR-R-05')?.message ?? null); this.snack.open('Reserve submitted', undefined, { duration: 3000 }); this.refresh(); if (result.approvalStatus === 'AutoApproved') this.pollPosting(); }, error: (error: HttpErrorResponse) => { this.busy.set(null); if (error.status === 422) this.formError.set(Object.values((error.error as Partial<ApiError>)?.errors ?? {}).flat()[0] ?? 'Reserve could not be submitted.'); } });
  }
  approve(row: ReserveHistory): void { if (!this.auth.canReviewReserve(row) || this.busy()) return; this.run(row, 'approve', this.api.approve(this.claim.id, row.id), true); }
  reject(row: ReserveHistory): void { if (!this.auth.canReviewReserve(row) || this.busy()) return; this.dialog.open<ReserveRejectDialogComponent, void, string>(ReserveRejectDialogComponent, { width: '420px' }).afterClosed().pipe(take(1), takeUntilDestroyed(this.destroyRef)).subscribe(reason => { if (reason?.trim()) this.run(row, 'reject', this.api.reject(this.claim.id, row.id, reason.trim())); }); }
  retract(row: ReserveHistory): void { if (this.auth.canRetractReserve(row) && !this.busy()) this.run(row, 'retract', this.api.retract(this.claim.id, row.id)); }
  retry(row: ReserveHistory): void { if (this.auth.canRetryPosting(row) && !this.busy()) this.run(row, 'retry', this.api.retryPosting(this.claim.id, row.id), true); }
  setOverride(): void { if (!this.auth.isManager() || !this.overrideRequired() || this.busy()) return; this.busy.set('override'); this.api.setOverride(this.claim.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.busy.set(null); this.claim = { ...this.claim, managerOverride: true }; this.warning.set(null); this.snack.open('Reserve override set', undefined, { duration: 3000 }); this.refresh(); }, error: () => this.busy.set(null) }); }
  private run(row: ReserveHistory, action: string, request: ReturnType<ReservesApiService['approve']>, poll = false): void { this.busy.set(row.id + ':' + action); request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.busy.set(null); this.warning.set(null); this.snack.open(`Reserve ${action}d`, undefined, { duration: 3000 }); this.refresh(); if (poll) this.pollPosting(); }, error: (error: HttpErrorResponse) => { this.busy.set(null); if (error.status === 422) this.warning.set(Object.values((error.error as Partial<ApiError>)?.errors ?? {}).flat().join(' ') || 'Reserve action could not be completed.'); } }); }
  private pollPosting(): void { timer(2000, 3000).pipe(take(6), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refresh()); }
}
