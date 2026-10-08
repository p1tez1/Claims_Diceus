import { ChangeDetectionStrategy, Component, Input, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption } from '@angular/material/core';
import { MatSelect } from '@angular/material/select';
import { AssetType, PartyInput, RiskObjectInput } from '../../core/models/api.models';
import { PartiesForm, nonBlank } from './fnol-forms';
import { PartyFormComponent } from '../../shared/party-form/party-form.component';

@Component({ selector: 'app-parties-step', standalone: true,
  imports: [ReactiveFormsModule, MatButton, MatError, MatFormField, MatLabel, MatInput, MatOption, MatSelect, PartyFormComponent],
  template: `
    <div class="section-heading"><h2>Parties</h2><button mat-stroked-button type="button" (click)="partyOpen.set(!partyOpen())">{{ partyOpen() ? 'Cancel' : 'Add Party' }}</button></div>
    @if (!hasClaimant()) { <p class="inline-warning">At least one Claimant is required before continuing.</p> }
    @for (party of form.controls.parties.value; track $index) { <div class="item-card"><div><strong>{{ party.type === 'Person' ? party.firstName + ' ' + party.lastName : party.companyName }}</strong><span>{{ party.role }} · {{ party.type }}</span><small>{{ party.email }} {{ party.phone }}</small>
      @for (message of itemErrors('Parties', $index); track message) { <small class="inline-warning">{{ message }}</small> }
    </div><button mat-button type="button" (click)="removeParty($index)">Remove</button></div> }
    @if (partyOpen()) { <app-party-form (partyAdded)="addParty($event)" /> }
    <div class="section-heading"><h2>Risk objects</h2><button mat-stroked-button type="button" (click)="riskOpen.set(!riskOpen())">{{ riskOpen() ? 'Cancel' : 'Add Risk Object' }}</button></div>
    @if (!form.controls.riskObjects.value.length) { <p class="advisory">No risk objects added. You can still create the claim; this will be recorded as a warning.</p> }
    @for (risk of form.controls.riskObjects.value; track $index) { <div class="item-card"><div><strong>{{ risk.assetType }} · {{ risk.assetDescription }}</strong><span>{{ risk.damageDescription }} {{ risk.assetReference }}</span>
      @for (message of itemErrors('RiskObjects', $index); track message) { <small class="inline-warning">{{ message }}</small> }
    </div><button mat-button type="button" (click)="removeRisk($index)">Remove</button></div> }
    @if (riskOpen()) { <form [formGroup]="riskForm" (ngSubmit)="addRisk()" class="step-grid inline-form">
      <mat-form-field><mat-label>Asset type</mat-label><mat-select formControlName="assetType">@for (type of assetTypes; track type) { <mat-option [value]="type">{{ type }}</mat-option> }</mat-select></mat-form-field>
      <mat-form-field><mat-label>Asset description</mat-label><input matInput formControlName="assetDescription">@if (riskForm.controls.assetDescription.invalid) { <mat-error>Description is required.</mat-error> }</mat-form-field>
      <mat-form-field><mat-label>Damage description</mat-label><input matInput formControlName="damageDescription"></mat-form-field>
      <mat-form-field><mat-label>Reference number</mat-label><input matInput formControlName="assetReference"></mat-form-field>
      <button mat-flat-button color="primary" type="submit">Add risk object</button>
    </form> }`, styleUrl: './fnol-intake.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class PartiesStepComponent {
  @Input({ required: true }) form!: PartiesForm;
  @Input() serverErrors: Record<string, string[]> = {};
  private readonly fb = inject(FormBuilder).nonNullable;
  readonly assetTypes: AssetType[] = ['Vehicle', 'Property', 'Person', 'Equipment', 'Other'];
  readonly partyOpen = signal(false);
  readonly riskOpen = signal(false);
  readonly riskForm = this.fb.group({ assetType: this.fb.control<AssetType>('Vehicle'), assetDescription: this.fb.control('', [nonBlank, Validators.maxLength(500)]), damageDescription: '', assetReference: this.fb.control('', Validators.maxLength(255)) });
  hasClaimant(): boolean { return this.form.controls.parties.value.some(party => party.role === 'Claimant'); }
  addParty(party: PartyInput): void { this.form.controls.parties.setValue([...this.form.controls.parties.value, party]); this.partyOpen.set(false); }
  removeParty(index: number): void { this.form.controls.parties.setValue(this.form.controls.parties.value.filter((_, i) => i !== index)); }
  addRisk(): void {
    if (this.riskForm.invalid || !this.riskForm.controls.assetDescription.value.trim()) { this.riskForm.markAllAsTouched(); return; }
    const r = this.riskForm.getRawValue();
    const risk: RiskObjectInput = { assetType: r.assetType, assetDescription: r.assetDescription.trim(), damageDescription: r.damageDescription.trim(), assetReference: r.assetReference.trim(), isPrimary: this.form.controls.riskObjects.value.length === 0 };
    this.form.controls.riskObjects.setValue([...this.form.controls.riskObjects.value, risk]); this.riskForm.reset({ assetType: 'Vehicle', assetDescription: '', damageDescription: '', assetReference: '' }); this.riskOpen.set(false);
  }
  removeRisk(index: number): void { const risks = this.form.controls.riskObjects.value.filter((_, i) => i !== index); this.form.controls.riskObjects.setValue(risks.map((risk, i) => ({ ...risk, isPrimary: i === 0 }))); }
  itemErrors(prefix: string, index: number): string[] { return Object.entries(this.serverErrors).filter(([key]) => key.startsWith(`${prefix}[${index}]`)).flatMap(([, value]) => value); }
}
