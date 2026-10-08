import { HttpErrorResponse, HttpEventType } from '@angular/common/http';
import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, of, throwError } from 'rxjs';
import { AuditLog, ClaimDetail, DocumentInfo, Page, ReserveComponent, ReserveHistory, User, UserRole } from '../../core/models/api.models';
import { AuditApiService } from '../../core/services/audit-api.service';
import { AuthService } from '../../core/services/auth.service';
import { DocumentsApiService } from '../../core/services/documents-api.service';
import { ReservesApiService } from '../../core/services/reserves-api.service';
import { ClaimAuditComponent } from './claim-audit.component';
import { ClaimDocumentsComponent } from './claim-documents.component';
import { ClaimReservesComponent } from './claim-reserves.component';
import { ReserveRejectDialogComponent } from './reserve-reject-dialog.component';

const handler: User = { id: 'handler', name: 'Handler', role: 'Handler' };
const supervisor: User = { id: 'supervisor', name: 'Supervisor', role: 'Supervisor' };
const manager: User = { id: 'manager', name: 'Manager', role: 'Manager' };
const transaction: ReserveHistory = { id: 'txn-1', transactionType: 'Add', amount: 50_000, changeSequence: 1, changeReason: 'Estimate', idempotencyKey: 'key', approvalStatus: 'PendingApproval', postingStatus: 'Pending', submittedByUserId: 'handler', createdAt: '2026-10-01T12:00:00Z' };
const reserve: ReserveComponent = { id: 'component', component: 'Indemnity', status: 'Active', currentAmount: 0, pendingAmount: 50_000, rowVersion: '', history: [transaction] };
const claim = { id: 'claim', policyId: 'policy', managerOverride: false } as ClaimDetail;

describe('Reserves tab', () => {
  let fixture: ComponentFixture<ClaimReservesComponent>;
  let component: ClaimReservesComponent;
  let api: jasmine.SpyObj<ReservesApiService>;
  let dialog: jasmine.SpyObj<MatDialog>;
  const user = signal<User | null>(handler);
  const auth = {
    currentUser: user,
    isSupervisorOrAbove: computed(() => user()?.role !== 'Handler'),
    isManager: computed(() => user()?.role === 'Manager'),
    hasRole: (required: UserRole) => ({ Handler: 0, Supervisor: 1, Manager: 2 })[user()?.role ?? 'Handler'] >= ({ Handler: 0, Supervisor: 1, Manager: 2 })[required],
    canReviewReserve: (row: ReserveHistory) => AuthService.prototype.canReviewReserve.call(auth as unknown as AuthService, row),
    reserveReviewBlockReason: (row: ReserveHistory) => AuthService.prototype.reserveReviewBlockReason.call(auth as unknown as AuthService, row),
    canRetractReserve: (row: ReserveHistory) => AuthService.prototype.canRetractReserve.call(auth as unknown as AuthService, row),
    canRetryPosting: (row: ReserveHistory) => AuthService.prototype.canRetryPosting.call(auth as unknown as AuthService, row)
  };

  beforeEach(() => {
    user.set(handler);
    api = jasmine.createSpyObj<ReservesApiService>('ReservesApiService', ['get', 'submit', 'approve', 'reject', 'retract', 'retryPosting', 'setOverride']);
    api.get.and.returnValue(of([reserve])); api.submit.and.returnValue(of({ transaction, approvalStatus: 'PendingApproval', warnings: [] }));
    api.approve.and.returnValue(of(void 0)); api.reject.and.returnValue(of(void 0)); api.retract.and.returnValue(of(void 0)); api.retryPosting.and.returnValue(of(void 0)); api.setOverride.and.returnValue(of(void 0));
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    TestBed.configureTestingModule({ imports: [ClaimReservesComponent], providers: [provideNoopAnimations(), { provide: AuthService, useValue: auth }, { provide: ReservesApiService, useValue: api }, { provide: MatDialog, useValue: dialog }, { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) }] });
    fixture = TestBed.createComponent(ClaimReservesComponent); component = fixture.componentInstance; component.claim = { ...claim }; fixture.detectChanges();
  });

  const actions = (fixture: ComponentFixture<ClaimReservesComponent>) => fixture.nativeElement.querySelector('.row-actions')?.textContent ?? '';
  it('gates review by role, ownership, and amount while showing retract only to the submitter', () => {
    expect(actions(fixture)).toContain('Retract'); expect(actions(fixture)).not.toContain('Approve');
    user.set(supervisor); fixture.detectChanges(); expect(actions(fixture)).toContain('Approve'); expect(actions(fixture)).not.toContain('Retract');
    expect(fixture.nativeElement.querySelector('.row-actions button').disabled).toBeFalse();
    user.set(manager); component.reserves.set([{ ...reserve, history: [{ ...transaction, submittedByUserId: 'manager' }] }]); fixture.detectChanges();
    expect(actions(fixture)).toContain('Retract'); expect(fixture.nativeElement.querySelector('.row-actions button').disabled).toBeTrue();
    component.reserves.set([{ ...reserve, history: [{ ...transaction, amount: 10_000_001, submittedByUserId: 'handler' }] }]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.row-actions button').disabled).toBeTrue();
    user.set(supervisor); component.reserves.set([{ ...reserve, history: [{ ...transaction, amount: 100_001 }] }]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.row-actions button').disabled).toBeTrue();
  });

  it('colors signed amounts and updates the authority indicator', () => {
    component.reserves.set([{ ...reserve, history: [transaction, { ...transaction, id: 'txn-2', amount: -500, createdAt: '2026-10-02T12:00:00Z' }] }]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('td.increase').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('td.decrease').length).toBe(1);
    component.panelOpen.set(true); component.form.controls.amount.setValue(50_000); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.authority').textContent).toContain('Supervisor approval required');
  });

  it('sends the reason returned by the reject dialog', () => {
    user.set(supervisor);
    dialog.open.and.returnValue({ afterClosed: () => of('Insufficient evidence') } as never);
    component.reject(transaction);
    expect(dialog.open).toHaveBeenCalledWith(ReserveRejectDialogComponent, jasmine.any(Object));
    expect(api.reject).toHaveBeenCalledWith('claim', 'txn-1', 'Insufficient evidence');
  });

  it('shows retry only for failed posting and override only for managers after a cap warning', () => {
    component.reserves.set([{ ...reserve, history: [{ ...transaction, postingStatus: 'Failed' }] }]); fixture.detectChanges(); expect(actions(fixture)).toContain('Retry');
    component.reserves.set([{ ...reserve, currentAmount: 9_990_000 }]); component.warning.set('Manager override required.'); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Set override');
    user.set(manager); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Set override');
  });

  it('blocks reserve submission without a policy', () => {
    fixture.componentRef.setInput('claim', { ...claim, policyId: undefined }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('A policy must be linked');
    expect(fixture.nativeElement.querySelector('.actions button:last-child').disabled).toBeTrue();
  });

  it('F04 exposes the override on a fresh manager visit from approved balances and a pending delta', () => {
    user.set(manager);
    component.reserves.set([{ ...reserve, currentAmount: 9_990_000 }]);
    fixture.detectChanges();
    expect(component.warning()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Set override');
    component.setOverride();
    expect(api.setOverride).toHaveBeenCalledWith('claim');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Set override');
  });

  it('F04 shows the server approval blocker', () => {
    user.set(manager);
    api.approve.and.returnValue(throwError(() => new HttpErrorResponse({ status: 422, error: { errors: { 'BR-R-05': ['Manager override is required.'] } } })));
    component.approve(transaction);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Manager override is required.');
    expect(component.busy()).toBeNull();
  });

  it('F13 blocks new reserves on terminal claims', () => {
    fixture.componentRef.setInput('claim', { ...claim, status: 'Closed' });
    component.form.patchValue({ amount: 100, changeReason: 'Estimate' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.actions button:last-child').disabled).toBeTrue();
    component.submit();
    expect(api.submit).not.toHaveBeenCalled();
  });

  it('F13 shows field errors for invalid amounts and blank reasons', () => {
    component.panelOpen.set(true);
    component.form.patchValue({ amount: 0, changeReason: '   ' }); component.form.markAllAsTouched();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('mat-error').length).toBeGreaterThanOrEqual(2);
    component.submit(); expect(api.submit).not.toHaveBeenCalled();
  });

  it('F21 orders each component by sequence even when timestamps disagree', () => {
    component.reserves.set([{ ...reserve, history: [transaction, { ...transaction, id: 'txn-2', changeSequence: 2, createdAt: '2026-09-01T00:00:00Z' }] }, { ...reserve, id: 'expense', component: 'Expense', history: [{ ...transaction, id: 'expense-1', changeSequence: 1 }] }]);
    expect(component.history().map(row => row.id)).toEqual(['txn-1', 'txn-2', 'expense-1']);
  });
});

const document: DocumentInfo = { id: 'doc', claimId: 'claim', documentType: 'Evidence', documentName: 'proof.pdf', blobPath: 'path', contentType: 'application/pdf', fileSizeBytes: 1024, uploadedAt: '2026-10-01T12:00:00Z', downloadUrl: 'https://example.test/proof.pdf' };
describe('Documents tab', () => {
  let component: ClaimDocumentsComponent;
  let fixture: ComponentFixture<ClaimDocumentsComponent>;
  let api: jasmine.SpyObj<DocumentsApiService>;
  beforeEach(() => {
    api = jasmine.createSpyObj<DocumentsApiService>('DocumentsApiService', ['list', 'uploadWithProgress']); api.list.and.returnValue(of([document]));
    TestBed.configureTestingModule({ imports: [ClaimDocumentsComponent], providers: [provideNoopAnimations(), { provide: DocumentsApiService, useValue: api }, { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) }] });
    fixture = TestBed.createComponent(ClaimDocumentsComponent); component = fixture.componentInstance; component.claimId = 'claim'; fixture.detectChanges();
  });
  it('rejects oversize and unsupported files before upload', () => {
    component.selectFile({ target: { files: [new File(['x'], 'bad.exe')] } } as unknown as Event);
    expect(component.selectedFile()).toBeNull(); expect(component.error()).toContain('Allowed extensions');
    component.selectFile({ target: { files: [{ name: 'large.pdf', size: 50 * 1024 * 1024 + 1 }] } } as unknown as Event);
    expect(component.error()).toContain('50 MB'); expect(api.uploadWithProgress).not.toHaveBeenCalled();
  });
  it('tracks upload progress and reports 422 errors', () => {
    const progress = new Subject<any>(); api.uploadWithProgress.and.returnValue(progress);
    component.selectedFile.set(new File(['content'], 'proof.pdf', { type: 'application/pdf' })); component.upload();
    progress.next({ type: HttpEventType.UploadProgress, loaded: 5, total: 10 }); expect(component.progress()).toBe(50); expect(component.uploading()).toBeTrue();
    progress.error(new HttpErrorResponse({ status: 422, error: { errors: { File: ['Invalid file content.'] } } }));
    expect(component.error()).toBe('Invalid file content.'); expect(component.uploading()).toBeFalse();
  });
  it('F14 obtains a fresh URL on click instead of opening an expired cached SAS', () => {
    const refreshed = new Subject<DocumentInfo[]>(); api.list.and.returnValue(refreshed);
    spyOn(window, 'open'); component.download(document);
    expect(api.list).toHaveBeenCalledTimes(2);
    expect(window.open).not.toHaveBeenCalled();
    refreshed.next([{ ...document, downloadUrl: 'https://example.test/fresh-sas' }]); refreshed.complete();
    expect(window.open).toHaveBeenCalledWith('https://example.test/fresh-sas', '_blank', 'noopener,noreferrer');
  });

  it('F14 displays download failures and permits a retry', () => {
    api.list.and.returnValue(throwError(() => new Error('Offline')));
    spyOn(window, 'open'); component.download(document);
    expect(component.error()).toContain('Download failed');
    expect(window.open).not.toHaveBeenCalled();
    api.list.and.returnValue(of([document])); component.download(document);
    expect(window.open).toHaveBeenCalledTimes(1);
  });
});

describe('Audit tab', () => {
  it('F22 routes party audit events to the Parties tab', () => {
    const api = jasmine.createSpyObj<AuditApiService>('AuditApiService', ['get']);
    TestBed.configureTestingModule({ imports: [ClaimAuditComponent], providers: [provideNoopAnimations(), { provide: AuditApiService, useValue: api }] });
    const component = TestBed.createComponent(ClaimAuditComponent).componentInstance;
    const selected = jasmine.createSpy('selected'); component.relatedSelected.subscribe(selected);
    const event = { id: 'event', claimId: 'claim', eventType: 'PARTY_REMOVED', description: 'Removed', createdAt: '', relatedEntityId: 'inactive-party', relatedEntityType: 'ClaimParty' };
    expect(component.relatedLink(event)).toBe('#party-inactive-party');
    component.openRelated(new Event('click'), event);
    expect(selected).toHaveBeenCalledWith({ tab: 1, id: 'party-inactive-party' });
  });
  it('loads reverse chronological pages and advances with the API', () => {
    const older: AuditLog = { id: 'old', claimId: 'claim', eventType: 'CLAIM_CREATED', description: 'Created', createdAt: '2026-10-01T00:00:00Z' };
    const newer: AuditLog = { ...older, id: 'new', eventType: 'RESERVE_CREATED', createdAt: '2026-10-02T00:00:00Z' };
    const api = jasmine.createSpyObj<AuditApiService>('AuditApiService', ['get']);
    api.get.and.returnValues(of({ items: [older, newer], totalCount: 21, page: 1, pageSize: 20 } as Page<AuditLog>), of({ items: [older], totalCount: 21, page: 2, pageSize: 20 } as Page<AuditLog>));
    TestBed.configureTestingModule({ imports: [ClaimAuditComponent], providers: [provideNoopAnimations(), { provide: AuditApiService, useValue: api }] });
    const fixture = TestBed.createComponent(ClaimAuditComponent); fixture.componentInstance.claimId = 'claim'; fixture.detectChanges();
    expect(fixture.componentInstance.items().map(item => item.id)).toEqual(['new', 'old']);
    fixture.componentInstance.load(2); expect(api.get).toHaveBeenCalledWith('claim', 2, 20); expect(fixture.componentInstance.page()).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('button').length).toBe(3);
  });
});
