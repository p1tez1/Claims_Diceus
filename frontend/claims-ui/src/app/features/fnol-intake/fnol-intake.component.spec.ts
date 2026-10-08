import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatStepper } from '@angular/material/stepper';
import { Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, Subject, throwError } from 'rxjs';
import { ClaimDetail, ClaimValidation } from '../../core/models/api.models';
import { ClaimsApiService } from '../../core/services/claims-api.service';
import { PoliciesApiService } from '../../core/services/policies-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { FnolIntakeComponent } from './fnol-intake.component';

describe('FNOL intake', () => {
  let component: FnolIntakeComponent;
  let api: jasmine.SpyObj<ClaimsApiService>;
  let dialog: jasmine.SpyObj<MatDialog>;
  let stepper: { selectedIndex: number; next: jasmine.Spy };
  const clean: ClaimValidation = { criticalIssues: [], warnings: [] };
  beforeEach(() => {
    api = jasmine.createSpyObj<ClaimsApiService>('ClaimsApiService', ['validate', 'create']);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    TestBed.configureTestingModule({ imports: [FnolIntakeComponent], providers: [
      { provide: ClaimsApiService, useValue: api }, { provide: MatDialog, useValue: dialog },
      { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) },
      { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) }
    ] });
    TestBed.overrideComponent(FnolIntakeComponent, { set: { template: '' } });
    component = TestBed.createComponent(FnolIntakeComponent).componentInstance;
    stepper = { selectedIndex: 0, next: jasmine.createSpy('next') };
    component.stepper = stepper as unknown as MatStepper;
    component.loss.patchValue({ unknownPolicy: true, lossDate: '2026-01-01T10:00', causeOfLossCode: 'FIRE', lossDescription: 'A sufficiently detailed loss.' });
    component.people.controls.parties.setValue([{ role: 'Claimant', type: 'Person', firstName: 'Ada', lastName: 'Smith' }]);
  });

  it('gates each step independently', () => {
    component.loss.controls.lossDate.setValue('2999-01-01T00:00');
    component.next(0); expect(stepper.next).not.toHaveBeenCalled();
    component.loss.controls.lossDate.setValue('2026-01-01T10:00');
    component.next(0); expect(stepper.next).toHaveBeenCalledTimes(1);
    component.people.controls.parties.setValue([]);
    component.next(1); expect(stepper.next).toHaveBeenCalledTimes(1);
    component.people.controls.parties.setValue([{ role: 'Claimant', type: 'Person', firstName: 'Ada', lastName: 'Smith' }]);
    component.next(1); expect(stepper.next).toHaveBeenCalledTimes(2);
  });

  it('requires confirmation for dry-run warnings before submitting', () => {
    const answer = new Subject<boolean>();
    api.validate.and.returnValue(of({ criticalIssues: [], warnings: [{ field: 'PolicyId', code: 'BR-C-06', message: 'No policy linked' }] }));
    dialog.open.and.returnValue({ afterClosed: () => answer.asObservable() } as never);
    api.create.and.returnValue(of({ id: 'claim-id', claimNumber: 'CLM-2026-0000001' } as ClaimDetail));
    component.create(); expect(dialog.open).toHaveBeenCalled(); expect(api.create).not.toHaveBeenCalled();
    answer.next(false); expect(api.create).not.toHaveBeenCalled();
    component.create(); answer.next(true); expect(api.create).toHaveBeenCalledTimes(1);
  });

  it('maps 422 errors to fields and the first affected step', () => {
    api.validate.and.returnValue(of(clean));
    api.create.and.returnValue(throwError(() => new HttpErrorResponse({ status: 422, error: { errors: { 'Parties[0].Email': ['Invalid email'], InitialReserve: ['Bad reserve'] } } })));
    component.create();
    expect(stepper.selectedIndex).toBe(1);
    expect(component.stepErrors()[1]).toEqual(['Invalid email']);
    expect(component.serverErrors()['Parties[0].Email']).toEqual(['Invalid email']);
  });

  it('reuses an idempotency key on retry and disables submission while pending', () => {
    const response = new Subject<ClaimDetail>();
    api.validate.and.returnValue(of(clean));
    api.create.and.returnValues(throwError(() => new HttpErrorResponse({ status: 503 })), response.asObservable());
    component.create(); component.create();
    expect(api.create).toHaveBeenCalledTimes(2);
    expect(api.create.calls.argsFor(0)[1]).toBe(api.create.calls.argsFor(1)[1]);
    expect(component.pending()).toBeTrue();
    component.create(); expect(api.create).toHaveBeenCalledTimes(2);
    response.next({ id: 'claim-id', claimNumber: 'CLM-2026-0000001' } as ClaimDetail); response.complete();
    expect(component.pending()).toBeFalse();
  });

  it('clears an initial reserve when Unknown Policy is selected', () => {
    component.loss.controls.unknownPolicy.setValue(false);
    component.review.patchValue({ enabled: true, amount: 500, changeReason: 'Initial estimate' });
    component.loss.controls.unknownPolicy.setValue(true);
    expect(component.review.controls.enabled.value).toBeFalse();
    expect(component.review.controls.amount.value).toBeNull();
  });
});

describe('FNOL intake rendered form', () => {
  it('renders three Material steps and loads searchable causes', () => {
    TestBed.configureTestingModule({ imports: [FnolIntakeComponent], providers: [
      provideNoopAnimations(),
      { provide: ClaimsApiService, useValue: jasmine.createSpyObj('ClaimsApiService', ['validate', 'create']) },
      { provide: PoliciesApiService, useValue: { search: () => of([]) } },
      { provide: ReferenceApiService, useValue: { causes: () => of([{ id: 'cause', code: 'COL-FIRE', name: 'Fire', perilCategory: 'Property', sortOrder: 1 }]) } },
      { provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open']) },
      { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) },
      { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) }
    ] });
    const fixture = TestBed.createComponent(FnolIntakeComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.mat-step-header').length).toBe(3);
    expect(fixture.componentInstance.loss.invalid).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Unknown Policy');
  });
});
