import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { Policy } from '../../core/models/api.models';
import { PoliciesApiService } from '../../core/services/policies-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { createLossForm } from './fnol-forms';
import { LossStepComponent } from './loss-step.component';

describe('Loss step policy lookup', () => {
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
