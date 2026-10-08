import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatOption } from '@angular/material/core';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSelect } from '@angular/material/select';
import { PartyInput, PartyRole, PartyType } from '../../core/models/api.models';
import { nonBlank } from '../../features/fnol-intake/fnol-forms';

@Component({
  selector: 'app-party-form', standalone: true,
  imports: [ReactiveFormsModule, MatButton, MatOption, MatError, MatFormField, MatLabel, MatInput, MatSelect],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" class="party-form">
      <mat-form-field><mat-label>Role</mat-label><mat-select formControlName="role">@for (role of roles; track role) { <mat-option [value]="role">{{ role }}</mat-option> }</mat-select></mat-form-field>
      <mat-form-field><mat-label>Type</mat-label><mat-select formControlName="type">@for (type of types; track type) { <mat-option [value]="type">{{ type }}</mat-option> }</mat-select></mat-form-field>
      @if (form.controls.type.value === 'Person') {
        <mat-form-field><mat-label>First name</mat-label><input matInput formControlName="firstName">@if (form.controls.firstName.invalid) { <mat-error>First name is required.</mat-error> }</mat-form-field>
        <mat-form-field><mat-label>Last name</mat-label><input matInput formControlName="lastName">@if (form.controls.lastName.invalid) { <mat-error>Last name is required.</mat-error> }</mat-form-field>
      } @else {
        <mat-form-field class="wide"><mat-label>Company name</mat-label><input matInput formControlName="companyName">@if (form.controls.companyName.invalid) { <mat-error>Company name is required.</mat-error> }</mat-form-field>
      }
      <mat-form-field><mat-label>Email</mat-label><input matInput type="email" formControlName="email">@if (form.controls.email.invalid) { <mat-error>Enter a valid email.</mat-error> }</mat-form-field>
      <mat-form-field><mat-label>Phone</mat-label><input matInput formControlName="phone"></mat-form-field>
      <div class="actions"><button mat-flat-button color="primary" type="submit" [disabled]="pending">Add party</button></div>
    </form>`,
  styles: [`
    .party-form { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:.6rem 1rem; border:1px solid var(--claims-border); padding:1rem; border-radius:.6rem; margin:.75rem 0; }
    .wide,.actions { grid-column:1/-1; }.actions { display:flex; justify-content:flex-end; }
    @media(max-width:650px){.party-form{grid-template-columns:1fr}}
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PartyFormComponent {
  @Input() pending = false;
  @Output() readonly partyAdded = new EventEmitter<PartyInput>();
  private readonly fb = inject(FormBuilder).nonNullable;
  readonly roles: PartyRole[] = ['Claimant', 'Insured', 'Witness', 'ThirdParty', 'Attorney'];
  readonly types: PartyType[] = ['Person', 'Company'];
  readonly form = this.fb.group({ role: this.fb.control<PartyRole>('Claimant'), type: this.fb.control<PartyType>('Person'), firstName: '', lastName: '', companyName: '', email: this.fb.control('', [Validators.email, Validators.maxLength(255)]), phone: this.fb.control('', Validators.maxLength(50)) });

  submit(): void {
    if (this.pending) return;
    const value = this.form.getRawValue();
    this.form.controls.firstName.setValidators(value.type === 'Person' ? [nonBlank, Validators.maxLength(100)] : []);
    this.form.controls.lastName.setValidators(value.type === 'Person' ? [nonBlank, Validators.maxLength(100)] : []);
    this.form.controls.companyName.setValidators(value.type === 'Company' ? [nonBlank, Validators.maxLength(255)] : []);
    this.form.controls.firstName.updateValueAndValidity(); this.form.controls.lastName.updateValueAndValidity(); this.form.controls.companyName.updateValueAndValidity();
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.partyAdded.emit({ role: value.role, type: value.type,
      ...(value.type === 'Person' ? { firstName: value.firstName.trim(), lastName: value.lastName.trim() } : { companyName: value.companyName.trim() }),
      ...(value.email.trim() ? { email: value.email.trim() } : {}), ...(value.phone.trim() ? { phone: value.phone.trim() } : {}) });
    this.form.reset({ role: 'Claimant', type: 'Person', firstName: '', lastName: '', companyName: '', email: '', phone: '' });
  }
}
