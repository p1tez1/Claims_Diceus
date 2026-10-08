import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption } from '@angular/material/core';
import { MatSelect } from '@angular/material/select';
import { Policy, ReserveComponentType } from '../../core/models/api.models';
import { authorityIndicator } from '../../shared/authority/authority-indicator.pipe';
import { LossForm, PartiesForm, ReviewForm } from './fnol-forms';

@Component({ selector: 'app-review-step', standalone: true,
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, MatCheckbox, MatError, MatFormField, MatLabel, MatInput, MatOption, MatSelect],
  template: `
    <h2>Initial reserve</h2>
    @if (loss.controls.unknownPolicy.value) { <p class="advisory">Initial reserve is unavailable until a policy is linked.</p> }
    @else { <div [formGroup]="form" class="step-grid">
      <mat-checkbox formControlName="enabled" class="wide">Add an initial reserve</mat-checkbox>
      @if (form.controls.enabled.value) {
        <mat-form-field><mat-label>Component</mat-label><mat-select formControlName="component">@for (component of components; track component) { <mat-option [value]="component">{{ component }}</mat-option> }</mat-select></mat-form-field>
        <mat-form-field><mat-label>Amount</mat-label><input matInput type="number" step="0.01" formControlName="amount">
          @if (serverErrors['InitialReserve.Amount']) { <mat-error>{{ serverErrors['InitialReserve.Amount'][0] }}</mat-error> }
        </mat-form-field>
        @if (form.hasError('reserveComplete') && form.touched) { <p class="inline-warning">Enter a {{ form.controls.component.value === 'SubrogationRecoverable' ? 'negative' : 'positive' }} amount and a reason.</p> }
        <mat-form-field class="wide"><mat-label>Change reason</mat-label><input matInput formControlName="changeReason">
          @if (form.hasError('reserveComplete')) { <mat-error>A reason is required for the initial reserve.</mat-error> }
          @if (serverErrors['InitialReserve.ChangeReason']) { <mat-error>{{ serverErrors['InitialReserve.ChangeReason'][0] }}</mat-error> }
        </mat-form-field>
        @if (form.controls.amount.value !== null) { <p class="authority" [class.warning]="authority().level !== 'auto'">{{ authority().level === 'auto' ? '✓' : '⚠' }} {{ authority().label }}</p> }
      }
    </div> }
    @if (serverErrors['InitialReserve']) { <p class="inline-warning">{{ serverErrors['InitialReserve'][0] }}</p> }
    <h2>Review claim</h2>
    <dl class="review-grid">
      <div><dt>Policy</dt><dd>{{ loss.controls.unknownPolicy.value ? 'Unknown policy' : policy?.policyNumber }} @if (policy) { <span class="review-line">{{ policy.clientName }} · {{ policy.effectiveDate | date:'mediumDate' }}–{{ policy.expirationDate | date:'mediumDate' }}</span> }</dd></div>
      <div><dt>Loss date</dt><dd>{{ loss.controls.lossDate.value | date:'medium' }}</dd></div>
      <div><dt>Cause</dt><dd>{{ loss.controls.causeOfLossCode.value }}</dd></div>
      <div><dt>Location</dt><dd>{{ loss.controls.lossLocation.value || '—' }}</dd></div>
      <div class="wide"><dt>Description</dt><dd>{{ loss.controls.lossDescription.value }}</dd></div>
      <div><dt>Estimated loss</dt><dd>{{ loss.controls.estimatedLossAmount.value === null ? '—' : (loss.controls.estimatedLossAmount.value | currency) }}</dd></div>
      <div><dt>Initial reserve</dt><dd>{{ form.controls.enabled.value && !loss.controls.unknownPolicy.value ? (form.controls.component.value + ' · ' + (form.controls.amount.value | currency) + ' · ' + form.controls.changeReason.value) : 'None' }}</dd></div>
      <div class="wide"><dt>Parties</dt><dd>@for (party of parties.controls.parties.value; track $index) { <span class="review-line">{{ party.role }}: {{ party.type === 'Person' ? party.firstName + ' ' + party.lastName : party.companyName }} · {{ party.email || 'No email' }} · {{ party.phone || 'No phone' }}</span> }</dd></div>
      <div class="wide"><dt>Risk objects</dt><dd>@for (risk of parties.controls.riskObjects.value; track $index) { <span class="review-line">{{ risk.assetType }}: {{ risk.assetDescription }} — {{ risk.damageDescription || 'Damage not specified' }} · {{ risk.assetReference || 'No reference' }}</span> } @empty { None }</dd></div>
    </dl>`, styleUrl: './fnol-intake.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class ReviewStepComponent {
  @Input({ required: true }) form!: ReviewForm;
  @Input({ required: true }) loss!: LossForm;
  @Input({ required: true }) parties!: PartiesForm;
  @Input() policy: Policy | null = null;
  @Input() serverErrors: Record<string, string[]> = {};
  readonly components: ReserveComponentType[] = ['Indemnity', 'Expense', 'ALAE', 'SubrogationRecoverable'];
  authority() { return authorityIndicator(this.form.controls.amount.value ?? 0); }
}
