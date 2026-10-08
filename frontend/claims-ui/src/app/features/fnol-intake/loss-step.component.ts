import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocomplete, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatFormField, MatHint, MatError, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption } from '@angular/material/core';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { CauseOfLoss, Policy } from '../../core/models/api.models';
import { PoliciesApiService } from '../../core/services/policies-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { LossForm, inForce } from './fnol-forms';
import { localDateTimeInputValue, MIN_USER_DATE } from '../../shared/validation/date.validators';

@Component({ selector: 'app-loss-step', standalone: true,
  imports: [DatePipe, ReactiveFormsModule, MatAutocomplete, MatAutocompleteTrigger, MatFormField, MatHint, MatError, MatLabel, MatInput, MatOption, MatSlideToggle],
  template: `
  <div [formGroup]="form" class="step-grid">
    <div class="wide">
      <mat-slide-toggle formControlName="unknownPolicy">Unknown Policy</mat-slide-toggle>
      @if (form.controls.unknownPolicy.value) { <p class="advisory">You can create the claim without a policy. Financial actions will need a policy link later.</p> }
    </div>
    @if (!form.controls.unknownPolicy.value) {
      @if (policySearchError()) { <p class="inline-warning wide" role="alert">Policy search failed. Type again to retry.</p> }
      <mat-form-field class="wide"><mat-label>Find policy by number or client</mat-label>
        <input matInput formControlName="policySearch" [matAutocomplete]="policyOptions" autocomplete="off">
        <mat-autocomplete #policyOptions="matAutocomplete" (optionSelected)="selectPolicy($event.option.value)">
          @for (policy of policies(); track policy.id) { <mat-option [value]="policy.policyNumber">{{ policy.policyNumber }} · {{ policy.clientName }} · {{ policy.effectiveDate | date:'mediumDate' }}–{{ policy.expirationDate | date:'mediumDate' }}</mat-option> }
        </mat-autocomplete>
      </mat-form-field>
      @if (form.hasError('policyRequired') && form.touched) { <p class="inline-warning wide">Select a policy or choose Unknown Policy.</p> }
      @if (serverErrors['PolicyId']) { <p class="inline-warning wide">{{ serverErrors['PolicyId'][0] }}</p> }
      @if (selectedPolicy()) { <div class="policy-card wide"><strong>{{ selectedPolicy()!.policyNumber }}</strong> · {{ selectedPolicy()!.clientName }}<br>
        {{ selectedPolicy()!.effectiveDate | date:'mediumDate' }}–{{ selectedPolicy()!.expirationDate | date:'mediumDate' }} · {{ selectedPolicy()!.coverageTypes.join(', ') }}
        @if (form.controls.lossDate.value) { <span class="force-badge" [class.outside]="!policyInForce()">{{ policyInForce() ? 'In force on loss date' : 'Outside policy period' }}</span> }
      </div> }
    }
    <mat-form-field><mat-label>Loss date and time</mat-label><input matInput type="datetime-local" formControlName="lossDate" [min]="minLossDate" [max]="maxLossDate">
      @if (form.controls.lossDate.hasError('required')) { <mat-error>Loss date and time are required.</mat-error> }
      @else if (form.controls.lossDate.hasError('dateRange')) { <mat-error>Enter a date between 1900 and 2100.</mat-error> }
      @else if (form.controls.lossDate.hasError('futureDate')) { <mat-error>Loss date cannot be in the future.</mat-error> }
      @if (serverErrors['LossDate']) { <mat-error>{{ serverErrors['LossDate'][0] }}</mat-error> }
    </mat-form-field>
    <mat-form-field><mat-label>Cause of loss</mat-label><input matInput formControlName="causeSearch" [matAutocomplete]="causeOptions" autocomplete="off">
      <mat-autocomplete #causeOptions="matAutocomplete" (optionSelected)="selectCause($event.option.value)">
        @for (cause of filteredCauses(); track cause.code) { <mat-option [value]="cause.code">{{ cause.code }} · {{ cause.name }}</mat-option> }
      </mat-autocomplete>
    </mat-form-field>
    @if (form.controls.causeOfLossCode.hasError('required') && form.touched) { <p class="inline-warning">Select a cause of loss.</p> }
    @if (serverErrors['CauseOfLossCode']) { <p class="inline-warning">{{ serverErrors['CauseOfLossCode'][0] }}</p> }
    <mat-form-field class="wide"><mat-label>Loss description</mat-label><textarea matInput rows="4" formControlName="lossDescription"></textarea>
      <mat-hint align="end">{{ form.controls.lossDescription.value.trim().length }}/20 minimum</mat-hint>
      @if (form.controls.lossDescription.hasError('minDescription')) { <mat-error>Enter at least 20 characters.</mat-error> }
      @if (serverErrors['LossDescription']) { <mat-error>{{ serverErrors['LossDescription'][0] }}</mat-error> }
    </mat-form-field>
    <mat-form-field><mat-label>Loss location</mat-label><input matInput formControlName="lossLocation">
      @if (serverErrors['LossLocation']) { <mat-error>{{ serverErrors['LossLocation'][0] }}</mat-error> }
    </mat-form-field>
    <mat-form-field><mat-label>Estimated loss amount</mat-label><input matInput type="number" min="0" step="0.01" formControlName="estimatedLossAmount">
      @if (form.controls.estimatedLossAmount.hasError('min')) { <mat-error>Amount cannot be negative.</mat-error> }
      @if (serverErrors['EstimatedLossAmount']) { <mat-error>{{ serverErrors['EstimatedLossAmount'][0] }}</mat-error> }
    </mat-form-field>
  </div>`, styleUrl: './fnol-intake.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class LossStepComponent {
  @Input({ required: true }) form!: LossForm;
  @Input() serverErrors: Record<string, string[]> = {};
  @Output() readonly policySelected = new EventEmitter<Policy | null>();
  private readonly policiesApi = inject(PoliciesApiService);
  private readonly referenceApi = inject(ReferenceApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly policies = signal<Policy[]>([]);
  readonly causes = signal<CauseOfLoss[]>([]);
  readonly selectedPolicy = signal<Policy | null>(null);
  readonly filteredCauses = signal<CauseOfLoss[]>([]);
  readonly policySearchError = signal(false);
  readonly minLossDate = `${MIN_USER_DATE}T00:00`;
  readonly maxLossDate = localDateTimeInputValue(new Date());
  ngOnInit(): void {
    this.referenceApi.causes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(causes => { this.causes.set(causes); this.filteredCauses.set(causes); });
    this.form.controls.policySearch.valueChanges.pipe(debounceTime(300), distinctUntilChanged(), switchMap(query => {
      this.policySearchError.set(false);
      return query.trim() ? this.policiesApi.search(query).pipe(catchError(() => { this.policySearchError.set(true); return of([]); })) : of([]);
    }), takeUntilDestroyed(this.destroyRef)).subscribe(policies => this.policies.set(policies));
    this.form.controls.policySearch.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      if (value !== this.selectedPolicy()?.policyNumber) { this.selectedPolicy.set(null); this.policySelected.emit(null); this.form.controls.policyId.setValue(''); }
    });
    this.form.controls.causeSearch.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      const term = value.toLowerCase();
      this.filteredCauses.set(this.causes().filter(cause => `${cause.code} ${cause.name}`.toLowerCase().includes(term)));
      if (value !== this.causes().find(cause => cause.code === this.form.controls.causeOfLossCode.value)?.code) this.form.controls.causeOfLossCode.setValue('');
    });
    this.form.controls.unknownPolicy.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(unknown => {
      if (unknown) { this.selectedPolicy.set(null); this.policySelected.emit(null); this.form.controls.policyId.setValue(''); this.form.controls.policySearch.setValue(''); }
      this.form.updateValueAndValidity();
    });
  }
  selectPolicy(number: string): void { const policy = this.policies().find(item => item.policyNumber === number); if (policy) { this.selectedPolicy.set(policy); this.policySelected.emit(policy); this.form.controls.policyId.setValue(policy.id); this.form.updateValueAndValidity(); } }
  selectCause(code: string): void { this.form.controls.causeOfLossCode.setValue(code); }
  policyInForce(): boolean { const policy = this.selectedPolicy(); return !!policy && inForce(this.form.controls.lossDate.value, policy.effectiveDate, policy.expirationDate); }
}
