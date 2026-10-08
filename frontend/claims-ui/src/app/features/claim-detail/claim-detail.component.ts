import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocomplete, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatButton } from '@angular/material/button';
import { MatOption } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTab, MatTabContent, MatTabGroup } from '@angular/material/tabs';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, catchError, debounceTime, distinctUntilChanged, finalize, forkJoin, of, switchMap, take } from 'rxjs';
import { ApiError, ClaimDetail, ClaimStatus, ClaimStatusReference, LiabilityDetermination, PartyInput, Policy, TransitionOptions, TransitionPreflight, User } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { ClaimsApiService } from '../../core/services/claims-api.service';
import { PoliciesApiService } from '../../core/services/policies-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { UsersApiService } from '../../core/services/users-api.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { LoadingIndicatorComponent } from '../../shared/loading/loading-indicator.component';
import { ClaimHeaderComponent } from './claim-header.component';
import { ClaimOverviewComponent } from './claim-overview.component';
import { ClaimPartiesComponent } from './claim-parties.component';
import { ClaimReservesComponent } from './claim-reserves.component';
import { ClaimDocumentsComponent } from './claim-documents.component';
import { ClaimAuditComponent } from './claim-audit.component';
import { TransitionDialogComponent, TransitionDialogData } from './transition-dialog.component';

@Component({ selector: 'app-claim-detail', standalone: true,
  imports: [ReactiveFormsModule, MatAutocomplete, MatAutocompleteTrigger, MatButton, MatFormField, MatInput, MatLabel, MatOption, MatTab, MatTabContent, MatTabGroup, ClaimHeaderComponent, ClaimOverviewComponent, ClaimPartiesComponent, ClaimReservesComponent, ClaimDocumentsComponent, ClaimAuditComponent, EmptyStateComponent, LoadingIndicatorComponent],
  template: `
    @if (loading()) { <section class="surface loading"><app-loading-indicator mode="skeleton" label="Loading claim" /></section> }
    @else { @if (claim(); as item) {
      <app-claim-header [claim]="item" [transitions]="validTransitions()" [assignedHandlerName]="assignedHandlerName()" [users]="users()" [canAssign]="auth.isSupervisorOrAbove()" [pending]="pendingAction() !== null" (copyRequested)="copyClaimNumber($event)" (transitionRequested)="beginTransition($event)" (assigneeChanged)="assignHandler($event)" />
      @if (!item.policyId) { <section class="policy-link surface"><div><strong>No policy linked</strong><span>Link a policy before creating reserves.</span></div><mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Search policy or client</mat-label><input matInput [formControl]="policySearch" [matAutocomplete]="policyOptions"><mat-autocomplete #policyOptions="matAutocomplete" [displayWith]="displayPolicy" (optionSelected)="selectedPolicy.set($event.option.value)">@for (policy of policyResults(); track policy.id) { <mat-option [value]="policy">{{ policy.policyNumber }} · {{ policy.clientName }}</mat-option> }</mat-autocomplete></mat-form-field><button mat-flat-button color="primary" [disabled]="!selectedPolicy() || pendingAction() !== null" (click)="linkPolicy()">Link policy</button></section> }
      @if (policySearchError()) { <p role="alert">Policy search failed. Type again to retry.</p> } <section class="detail-tabs surface"><mat-tab-group animationDuration="150ms" [selectedIndex]="selectedTab()" (selectedIndexChange)="selectedTab.set($event)">
        <mat-tab label="Overview"><ng-template matTabContent><div class="tab-body"><app-claim-overview [claim]="item" [pending]="pendingAction() !== null" (notesSaved)="saveNotes($event)" (liabilitySaved)="saveLiability($event)" /></div></ng-template></mat-tab>
        <mat-tab label="Parties"><ng-template matTabContent><div class="tab-body"><app-claim-parties [parties]="item.parties" [pending]="pendingAction() !== null" [focusId]="relatedFocus()" (partyAdded)="addParty($event)" (partyRemoved)="removeParty($event)" /></div></ng-template></mat-tab>
        <mat-tab label="Reserves"><ng-template matTabContent><div class="tab-body"><app-claim-reserves [claim]="item" [focusId]="relatedFocus()" /></div></ng-template></mat-tab>
        <mat-tab label="Documents"><ng-template matTabContent><div class="tab-body"><app-claim-documents [claimId]="item.id" [focusId]="relatedFocus()" /></div></ng-template></mat-tab>
        <mat-tab label="Audit Log"><ng-template matTabContent><div class="tab-body"><app-claim-audit [claimId]="item.id" (relatedSelected)="openRelated($event)" /></div></ng-template></mat-tab>
      </mat-tab-group></section>
    } @else { <section class="surface"><app-empty-state title="Claim not available" message="The claim could not be loaded. Return to the claims list and try again." /></section> } }
  `,
  styles: [`
    :host{display:block}.loading{padding:1.5rem}.detail-tabs{margin-top:1rem;overflow:hidden}.tab-body{padding:1.25rem;min-height:300px}.policy-link{display:flex;align-items:center;gap:1rem;min-height:auto;margin-top:1rem;padding:.85rem 1.25rem;border-color:#fed7aa;background:#fffaf3}.policy-link>div{min-width:14rem}.policy-link span{display:block;color:var(--claims-text-muted);font-size:.85rem;margin-top:.2rem}.policy-link mat-form-field{flex:1}@media(max-width:700px){.policy-link{align-items:stretch;flex-direction:column}}
  `], changeDetection: ChangeDetectionStrategy.OnPush })
export class ClaimDetailComponent {
  private readonly api = inject(ClaimsApiService);
  private readonly references = inject(ReferenceApiService);
  private readonly policies = inject(PoliciesApiService);
  private readonly usersApi = inject(UsersApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);
  readonly claim = signal<ClaimDetail | null>(null);
  readonly statuses = signal<ClaimStatusReference[]>([]);
  readonly users = signal<User[]>([]);
  readonly loading = signal(true);
  readonly selectedTab = signal(0);
  readonly relatedFocus = signal<string | null>(null);
  readonly policySearch = new FormControl<string | Policy>('', { nonNullable: true });
  readonly policyResults = signal<Policy[]>([]);
  readonly selectedPolicy = signal<Policy | null>(null);
  readonly policySearchError = signal(false);
  readonly pendingAction = signal<string | null>(null);
  private readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  readonly assignedHandlerName = computed(() => this.users().find(user => user.id === this.claim()?.assignedHandlerId)?.name ?? '');
  readonly validTransitions = computed(() => {
    const claim = this.claim(); if (!claim) return [];
    const reference = this.statuses().find(item => item.status === claim.status);
    return (reference?.transitions ?? []).filter(edge => this.auth.hasRole(edge.requiredRole)).map(edge => edge.to);
  });

  constructor() {
    forkJoin({ claim: this.api.get(this.id), statuses: this.references.claimStatuses(), users: this.usersApi.list() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.claim.set(result.claim); this.statuses.set(result.statuses); this.users.set(result.users); this.loading.set(false); this.showCreationSuccess(result.claim); }, error: () => this.loading.set(false) });
    this.policySearch.valueChanges.pipe(debounceTime(250), distinctUntilChanged(), switchMap(value => {
      this.policySearchError.set(false);
      if (typeof value === 'string') this.selectedPolicy.set(null);
      return typeof value === 'string' && value.trim().length >= 2
        ? this.policies.search(value.trim()).pipe(catchError(() => { this.policySearchError.set(true); return of([]); })) : of([]);
    }), takeUntilDestroyed(this.destroyRef)).subscribe(results => this.policyResults.set(results));
  }

  readonly displayPolicy = (policy: Policy | string | null): string => typeof policy === 'string' ? policy : policy ? `${policy.policyNumber} · ${policy.clientName}` : '';
  copyClaimNumber(value: string): void { void navigator.clipboard.writeText(value).then(() => this.snack.open('Claim number copied', undefined, { duration: 2500 })); }
  saveNotes(notes?: string): void { this.mutate('notes', () => this.api.updateNotes(this.id, notes), 'Claim notes saved'); }
  saveLiability(determination: LiabilityDetermination): void { this.mutate('liability', () => this.api.updateLiability(this.id, determination), 'Liability determination saved'); }
  addParty(party: PartyInput): void { this.mutate('party', () => this.api.addParty(this.id, party, crypto.randomUUID()), 'Party added'); }
  removeParty(partyId: string): void { this.mutate('party', () => this.api.removeParty(this.id, partyId), 'Party removed'); }
  assignHandler(userId: string): void { if (userId !== this.claim()?.assignedHandlerId) this.mutate('assignee', () => this.api.assignHandler(this.id, userId), 'Assigned handler updated'); }
  linkPolicy(): void { const policy = this.selectedPolicy(); if (policy) this.mutate('policy', () => this.api.linkPolicy(this.id, policy.id), `Policy ${policy.policyNumber} linked`, () => this.policySearch.setValue('')); }
  openRelated(target: { tab: number; id: string }): void { this.relatedFocus.set(target.id); this.selectedTab.set(target.tab); }

  beginTransition(target: ClaimStatus): void {
    if (this.pendingAction()) return;
    this.pendingAction.set('status');
    this.api.transitionPreflight(this.id, target).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: preflight => this.openTransitionDialog(target, preflight), error: () => this.pendingAction.set(null) });
  }

  private openTransitionDialog(target: ClaimStatus, preflight: TransitionPreflight, submitErrors?: string[]): void {
    this.dialog.open<TransitionDialogComponent, TransitionDialogData, TransitionOptions>(TransitionDialogComponent, { width: '560px', data: { target, preflight, submitErrors } }).afterClosed().pipe(take(1), takeUntilDestroyed(this.destroyRef)).subscribe(options => {
      if (!options) { this.pendingAction.set(null); return; }
      this.api.transitionPreflight(this.id, target, options).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: updated => {
        if (!updated.allowed) { this.openTransitionDialog(target, updated); return; }
        this.applyTransition(target, options, updated);
      }, error: () => this.pendingAction.set(null) });
    });
  }

  private applyTransition(target: ClaimStatus, options: TransitionOptions, preflight: TransitionPreflight): void {
    this.api.transition(this.id, target, options).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.snack.open(`Claim moved to ${result.status}`, undefined, { duration: 3500 }); this.refresh(); }, error: (error: HttpErrorResponse) => {
      if (error.status !== 422) { this.pendingAction.set(null); return; }
      const body = error.error as Partial<ApiError>; const errors = Object.entries(body.errors ?? {}).filter(([key]) => key !== 'ValidNextStatuses').flatMap(([, messages]) => messages);
      this.openTransitionDialog(target, preflight, errors);
    } });
  }

  private mutate(action: string, request: () => Observable<unknown>, message: string, onSuccess?: () => void): void {
    if (this.pendingAction()) return;
    this.pendingAction.set(action);
    request().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => {
      this.snack.open(message, undefined, { duration: 3000 }); onSuccess?.(); this.refresh();
    }, error: () => this.pendingAction.set(null) });
  }
  private refresh(): void { this.api.get(this.id).pipe(finalize(() => this.pendingAction.set(null)), takeUntilDestroyed(this.destroyRef)).subscribe({ next: claim => this.claim.set(claim), error: () => {} }); }
  private showCreationSuccess(claim: ClaimDetail): void {
    const created = this.route.snapshot.queryParamMap.get('created') === 'true' || history.state?.created === true;
    if (!created) return;
    this.snack.open(`Claim ${history.state?.claimNumber ?? claim.claimNumber} created successfully`, 'Dismiss', { duration: 7000 });
    void this.router.navigate([], { relativeTo: this.route, queryParams: { created: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
