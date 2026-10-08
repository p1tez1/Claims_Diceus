import { signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA, MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { NEVER, Subject, of, throwError } from 'rxjs';
import { ClaimDetail, ClaimStatusReference, Policy, TransitionPreflight, User } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { ClaimsApiService } from '../../core/services/claims-api.service';
import { PoliciesApiService } from '../../core/services/policies-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { UsersApiService } from '../../core/services/users-api.service';
import { ClaimDetailComponent } from './claim-detail.component';
import { ClaimHeaderComponent } from './claim-header.component';
import { ClaimOverviewComponent } from './claim-overview.component';
import { ClaimPartiesComponent } from './claim-parties.component';
import { TransitionDialogComponent, TransitionDialogData } from './transition-dialog.component';

const users: User[] = [
  { id: 'handler-id', name: 'Claims Handler', role: 'Handler' },
  { id: 'supervisor-id', name: 'Claims Supervisor', role: 'Supervisor' },
  { id: 'manager-id', name: 'Claims Manager', role: 'Manager' }
];
const claim: ClaimDetail = {
  id: 'claim-id', claimNumber: 'CLM-2026-0000042', status: 'Draft', severity: 'Standard', reportedDate: '2026-10-01T10:00:00Z', assignedHandlerId: 'handler-id', managerOverride: false, createdAt: '2026-10-01T10:00:00Z', rowVersion: '',
  lossEvent: { id: 'loss-id', lossDate: '2026-09-30T10:00:00Z', lossDescription: 'A detailed description of the insured loss.', lossLocation: 'Kyiv', causeOfLossCode: 'COL-FIRE', estimatedLossAmount: 12500, reportDate: '2026-10-01T10:00:00Z' },
  parties: [{ id: 'claimant-id', role: 'Claimant', type: 'Person', firstName: 'Ada', lastName: 'Smith', email: 'ada@example.test', isActive: true }],
  riskObjects: [], validationIssues: [{ id: 'issue-id', code: 'POLICY_UNKNOWN', message: 'No policy linked.', severity: 'Warning', status: 'Open', createdAt: '2026-10-01T10:00:00Z' }], reserves: [], documents: [], audit: []
};
const references: ClaimStatusReference[] = [
  { status: 'Draft', transitions: [{ to: 'Open', requiredRole: 'Handler', ruleCode: 'OPEN' }, { to: 'Withdrawn', requiredRole: 'Handler', ruleCode: 'WITHDRAW' }] },
  { status: 'Closed', transitions: [{ to: 'Reopened', requiredRole: 'Supervisor', ruleCode: 'REOPEN' }] }
];
const allowed: TransitionPreflight = { allowed: true, blockingConditions: [], warnings: [], requiresJustification: false, requiresReason: false };

describe('Claim detail container', () => {
  let fixture: ComponentFixture<ClaimDetailComponent>;
  let component: ClaimDetailComponent;
  let api: jasmine.SpyObj<ClaimsApiService>;
  let dialog: jasmine.SpyObj<MatDialog>;
  let role: 'Handler' | 'Supervisor' | 'Manager';
  let policies: jasmine.SpyObj<PoliciesApiService>;

  beforeEach(() => {
    role = 'Handler';
    api = jasmine.createSpyObj<ClaimsApiService>('ClaimsApiService', ['get', 'updateNotes', 'updateLiability', 'addParty', 'removeParty', 'assignHandler', 'linkPolicy', 'transitionPreflight', 'transition']);
    api.get.and.returnValue(of(claim)); api.updateNotes.and.returnValue(of(void 0)); api.linkPolicy.and.returnValue(of(void 0));
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    dialog.open.and.returnValue({ afterClosed: () => NEVER } as never);
    const ranks = { Handler: 0, Supervisor: 1, Manager: 2 };
    const auth = { currentUser: signal(users[0]), isSupervisorOrAbove: signal(false), hasRole: (required: keyof typeof ranks) => ranks[role] >= ranks[required] };
    policies = jasmine.createSpyObj<PoliciesApiService>('PoliciesApiService', ['search']); policies.search.and.returnValue(of([]));
    TestBed.configureTestingModule({ imports: [ClaimDetailComponent], providers: [provideNoopAnimations(),
      { provide: ClaimsApiService, useValue: api }, { provide: ReferenceApiService, useValue: { claimStatuses: () => of(references) } },
      { provide: UsersApiService, useValue: { list: () => of(users) } }, { provide: PoliciesApiService, useValue: policies },
      { provide: AuthService, useValue: auth }, { provide: MatDialog, useValue: dialog }, { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'claim-id' }, queryParamMap: { get: () => null } } } }, { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) }
    ] });
    TestBed.overrideComponent(ClaimDetailComponent, { set: { template: '' } });
    fixture = TestBed.createComponent(ClaimDetailComponent); component = fixture.componentInstance; fixture.detectChanges();
  });

  it('derives transition options from current status and role', () => {
    expect(component.validTransitions()).toEqual(['Open', 'Withdrawn']);
    component.claim.set({ ...claim, status: 'Closed' });
    expect(component.validTransitions()).toEqual([]);
    role = 'Supervisor';
    component.claim.set({ ...claim, status: 'Draft' }); component.claim.set({ ...claim, status: 'Closed' });
    expect(component.validTransitions()).toEqual(['Reopened']);
  });

  it('opens the preflight dialog with blocking conditions and warnings', () => {
    const preflight: TransitionPreflight = { allowed: false, blockingConditions: [{ code: 'BR-ST-02', message: 'Acknowledge all Open warnings before Open.' }], warnings: [{ code: 'POLICY_UNKNOWN', message: 'No policy linked.' }], requiresJustification: false, requiresReason: false };
    api.transitionPreflight.and.returnValue(of(preflight)); component.beginTransition('Open');
    expect(dialog.open).toHaveBeenCalledWith(TransitionDialogComponent, jasmine.objectContaining({ data: jasmine.objectContaining({ target: 'Open', preflight }) }));
  });

  it('saves notes and links a selected policy, refreshing after each action', () => {
    component.saveNotes('Updated notes');
    expect(api.updateNotes).toHaveBeenCalledWith('claim-id', 'Updated notes');
    const policy = { id: 'policy-id', policyNumber: 'POL-1', clientName: 'Ada Ltd' } as Policy;
    component.selectedPolicy.set(policy); component.linkPolicy();
    expect(api.linkPolicy).toHaveBeenCalledWith('claim-id', 'policy-id');
    expect(api.get).toHaveBeenCalledTimes(3);
  });

  it('F12 keeps policy typeahead alive after an HTTP failure', fakeAsync(() => {
    const policy = { id: 'policy', policyNumber: 'POL-1', clientName: 'Client' } as Policy;
    policies.search.and.returnValues(throwError(() => new Error('Offline')), of([policy]));
    component.policySearch.setValue('first'); tick(250);
    expect(component.policyResults()).toEqual([]);
    component.policySearch.setValue('second'); tick(250);
    expect(component.policyResults()).toEqual([policy]);
  }));

  it('F13 guards repeated detail writes until the response and refresh finish', () => {
    const response = new Subject<void>(), reload = new Subject<ClaimDetail>();
    api.updateNotes.and.returnValue(response); api.get.and.returnValue(reload);
    component.saveNotes('Note'); component.saveNotes('Note');
    expect(api.updateNotes).toHaveBeenCalledTimes(1);
    response.next(); response.complete();
    component.saveNotes('Note');
    expect(api.updateNotes).toHaveBeenCalledTimes(1);
    reload.next({ ...claim, notes: 'Note' }); reload.complete();
    component.saveNotes('Another note');
    expect(api.updateNotes).toHaveBeenCalledTimes(2);
  });

  it('F13 releases a failed write guard and prevents duplicate transition preflights', () => {
    api.updateNotes.and.returnValue(throwError(() => new Error('Offline')));
    component.saveNotes('Note'); component.saveNotes('Retry');
    expect(api.updateNotes).toHaveBeenCalledTimes(2);
    api.transitionPreflight.and.returnValue(NEVER);
    component.beginTransition('Open'); component.beginTransition('Open');
    expect(api.transitionPreflight).toHaveBeenCalledTimes(1);
  });
});

describe('Claim detail presentational components', () => {
  it('renders the always-visible header fields', () => {
    TestBed.configureTestingModule({ imports: [ClaimHeaderComponent], providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(ClaimHeaderComponent); fixture.componentInstance.claim = claim; fixture.componentInstance.assignedHandlerName = 'Claims Handler'; fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('CLM-2026-0000042'); expect(text).toContain('Draft'); expect(text).toContain('Claims Handler'); expect(text).toContain('COL-FIRE');
  });

  it('emits edited notes', () => {
    TestBed.configureTestingModule({ imports: [ClaimOverviewComponent], providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(ClaimOverviewComponent); fixture.componentInstance.claim = claim; const saved = jasmine.createSpy('saved'); fixture.componentInstance.notesSaved.subscribe(saved);
    fixture.componentInstance.notes.setValue('Investigation note'); fixture.componentInstance.notesSaved.emit(fixture.componentInstance.notes.value); expect(saved).toHaveBeenCalledWith('Investigation note');
  });

  it('adds parties and disables removal of the last active claimant', () => {
    TestBed.configureTestingModule({ imports: [ClaimPartiesComponent], providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(ClaimPartiesComponent); fixture.componentInstance.parties = claim.parties; fixture.detectChanges();
    const remove = fixture.nativeElement.querySelector('button[color="warn"]') as HTMLButtonElement;
    expect(remove.disabled).toBeTrue();
    const added = jasmine.createSpy('added'); fixture.componentInstance.partyAdded.subscribe(added); const party = { role: 'Witness', type: 'Person', firstName: 'Lin', lastName: 'Doe' } as const; fixture.componentInstance.add(party); expect(added).toHaveBeenCalledWith(party);
  });
});

describe('Transition dialog requirements', () => {
  function create(data: TransitionDialogData): TransitionDialogComponent {
    TestBed.configureTestingModule({ imports: [TransitionDialogComponent], providers: [provideNoopAnimations(), { provide: MAT_DIALOG_DATA, useValue: data }] });
    return TestBed.createComponent(TransitionDialogComponent).componentInstance;
  }

  it('requires a reason for Withdrawn and Reopened', () => {
    for (const target of ['Withdrawn', 'Reopened'] as const) {
      TestBed.resetTestingModule(); const component = create({ target, preflight: { ...allowed, allowed: false, requiresReason: true, blockingConditions: [{ code: target === 'Reopened' ? 'BR-ST-04' : 'BR-ST-01', message: `${target} reason is required.` }] } });
      expect(component.confirmDisabled).toBeTrue(); component.reason.setValue('Documented reason'); expect(component.confirmDisabled).toBeFalse();
    }
  });

  it('requires closure justification and keeps unrelated blockers disabled', () => {
    const component = create({ target: 'Closed', preflight: { ...allowed, allowed: false, requiresJustification: true, blockingConditions: [{ code: 'CC-04', message: 'A justification is required to close with non-zero reserves.' }] } });
    expect(component.confirmDisabled).toBeTrue(); component.justification.setValue('Reserve retained for final invoice.'); expect(component.confirmDisabled).toBeFalse();
    TestBed.resetTestingModule(); const blocked = create({ target: 'Closed', preflight: { ...allowed, allowed: false, blockingConditions: [{ code: 'CC-01', message: 'Pending reserve approvals remain.' }] } }); expect(blocked.confirmDisabled).toBeTrue();
  });

  it('requires warning acknowledgement for Draft to Open', () => {
    const component = create({ target: 'Open', preflight: { ...allowed, allowed: false, warnings: [{ code: 'POLICY_UNKNOWN', message: 'No policy linked.' }], blockingConditions: [{ code: 'BR-ST-02', message: 'Acknowledge all Open warnings before Open.' }] } });
    expect(component.confirmDisabled).toBeTrue(); component.acknowledge.setValue(true); expect(component.confirmDisabled).toBeFalse();
  });
});
