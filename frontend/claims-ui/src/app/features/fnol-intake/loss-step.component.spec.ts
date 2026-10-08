import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { Policy } from '../../core/models/api.models';
import { PoliciesApiService } from '../../core/services/policies-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { createLossForm } from './fnol-forms';
import { LossStepComponent } from './loss-step.component';

describe('Loss step policy lookup', () => {
  it('sets date input bounds and renders the range error', () => {
    TestBed.configureTestingModule({ imports: [LossStepComponent], providers: [provideNoopAnimations(),
      { provide: PoliciesApiService, useValue: { search: () => of([]) } },
      { provide: ReferenceApiService, useValue: { causes: () => of([]) } }] });
    const fixture = TestBed.createComponent(LossStepComponent), component = fixture.componentInstance;
    component.form = createLossForm();
    component.form.controls.lossDate.setValue('222222-02-22T10:00');
    component.form.controls.lossDate.markAsTouched();
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input[type="datetime-local"]') as HTMLInputElement;
    expect(input.min).toBe('1900-01-01T00:00');
    expect(input.max).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);
    expect(fixture.nativeElement.textContent).toContain('Enter a date between 1900 and 2100.');
    fixture.destroy();
  });

  it('F12 recovers after a failed search on the next query', fakeAsync(() => {
    const policy = { id: 'policy', policyNumber: 'POL-1', clientName: 'Client' } as Policy;
    const api = jasmine.createSpyObj<PoliciesApiService>('PoliciesApiService', ['search']);
    api.search.and.returnValues(throwError(() => new Error('Offline')), of([policy]));
    TestBed.configureTestingModule({ imports: [LossStepComponent], providers: [provideNoopAnimations(), { provide: PoliciesApiService, useValue: api }, { provide: ReferenceApiService, useValue: { causes: () => of([]) } }] });
    const fixture = TestBed.createComponent(LossStepComponent), component = fixture.componentInstance;
    component.form = createLossForm(); fixture.detectChanges();
    component.form.controls.policySearch.setValue('first'); tick(300);
    expect(component.policies()).toEqual([]);
    component.form.controls.policySearch.setValue('second'); tick(300);
    expect(component.policies()).toEqual([policy]);
    fixture.destroy();
  }));
});
