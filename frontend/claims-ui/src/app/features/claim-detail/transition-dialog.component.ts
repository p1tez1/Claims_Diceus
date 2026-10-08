import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle } from '@angular/material/dialog';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { ClaimStatus, TransitionOptions, TransitionPreflight } from '../../core/models/api.models';
import { statusPresentation } from '../../shared/status-badge/status-badge.component';
import { nonBlank } from '../fnol-intake/fnol-forms';

export interface TransitionDialogData { target: ClaimStatus; preflight: TransitionPreflight; submitErrors?: string[]; }

@Component({ selector: 'app-transition-dialog', standalone: true, imports: [ReactiveFormsModule, MatButton, MatCheckbox, MatError, MatFormField, MatLabel, MatInput, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle], template: `
  <h2 mat-dialog-title>Transition to {{ label }}</h2><mat-dialog-content>
    @if (data.preflight.blockingConditions.length) { <section class="conditions blocking"><h3>Blocking conditions</h3><ul>@for (condition of data.preflight.blockingConditions; track condition.code + condition.message) { <li><strong>{{ condition.code }}</strong> {{ condition.message }}</li> }</ul></section> }
    @if (data.submitErrors?.length) { <section class="conditions blocking"><h3>Transition could not be completed</h3><ul>@for (message of data.submitErrors; track message) { <li>{{ message }}</li> }</ul></section> }
    @if (data.preflight.warnings.length) { <section class="conditions warning"><h3>Warnings</h3><ul>@for (warning of data.preflight.warnings; track warning.code + warning.message) { <li><strong>{{ warning.code }}</strong> {{ warning.message }}</li> }</ul></section> }
    @if (data.target === 'Closed') { <h3>Closure checklist</h3><ul class="checklist"><li [class.met]="!hasBlock('CC-01')">No pending reserve approvals</li><li [class.met]="!hasBlock('CC-02')">No unresolved Critical issues</li><li [class.met]="!hasBlock('CC-03')">An active Claimant remains</li><li [class.met]="!hasBlock('CC-04')">Non-zero reserves justified</li></ul> }
    @if (data.preflight.requiresReason) { <mat-form-field appearance="outline"><mat-label>{{ data.target === 'Reopened' ? 'Reopen reason' : 'Withdrawal reason' }}</mat-label><textarea matInput rows="3" maxlength="2000" [formControl]="reason"></textarea>@if (reason.invalid) { <mat-error>A reason of up to 2000 characters is required.</mat-error> }</mat-form-field> }
    @if (data.preflight.requiresJustification) { <mat-form-field appearance="outline"><mat-label>Closure justification</mat-label><textarea matInput rows="3" maxlength="2000" [formControl]="justification"></textarea>@if (justification.invalid) { <mat-error>A justification of up to 2000 characters is required.</mat-error> }</mat-form-field> }
    @if (data.preflight.warnings.length && data.target === 'Open') { <mat-checkbox [formControl]="acknowledge">I acknowledge these warnings.</mat-checkbox> }
  </mat-dialog-content><mat-dialog-actions align="end"><button mat-button [mat-dialog-close]="undefined">Cancel</button><button mat-flat-button color="primary" [disabled]="confirmDisabled" [mat-dialog-close]="result">Confirm transition</button></mat-dialog-actions>
  `, styles: [`mat-dialog-content{min-width:min(32rem,80vw)}.conditions{padding:.75rem 1rem;border-radius:.5rem;margin-bottom:1rem}.conditions h3{margin:0 0 .35rem}.conditions ul,.checklist{margin:.25rem 0;padding-left:1.2rem}.blocking{background:#fef2f2;color:#991b1b}.warning{background:#fff7ed;color:#92400e}.checklist li{color:#991b1b}.checklist li.met{color:#166534}.checklist li.met::marker{content:'✓  '}.checklist li:not(.met)::marker{content:'×  '}mat-form-field{display:block;width:100%;margin-top:.75rem}`], changeDetection: ChangeDetectionStrategy.OnPush })
export class TransitionDialogComponent {
  readonly data = inject<TransitionDialogData>(MAT_DIALOG_DATA);
  readonly label = statusPresentation(this.data.target).label;
  readonly reason = new FormControl('', { nonNullable: true, validators: this.data.preflight.requiresReason ? [nonBlank, Validators.maxLength(2000)] : [] });
  readonly justification = new FormControl('', { nonNullable: true, validators: this.data.preflight.requiresJustification ? [nonBlank, Validators.maxLength(2000)] : [] });
  readonly acknowledge = new FormControl(false, { nonNullable: true });
  hasBlock(code: string): boolean { return this.data.preflight.blockingConditions.some(condition => condition.code === code); }
  get confirmDisabled(): boolean { return this.data.preflight.blockingConditions.some(condition => !this.inputResolvable(condition)) || this.reason.invalid || this.justification.invalid || (this.data.preflight.warnings.length > 0 && this.data.target === 'Open' && !this.acknowledge.value); }
  get result(): TransitionOptions { return { ...(this.reason.value.trim() ? { reason: this.reason.value.trim() } : {}), ...(this.justification.value.trim() ? { closureJustification: this.justification.value.trim() } : {}), acknowledgeWarnings: this.acknowledge.value }; }
  private inputResolvable(condition: { code: string; message?: string }): boolean { const message = condition.message?.toLowerCase() ?? ''; return (this.data.preflight.requiresReason && ['BR-ST-01', 'BR-ST-04'].includes(condition.code) && message.includes('reason')) || (this.data.preflight.requiresJustification && condition.code === 'CC-04' && message.includes('justification')) || (this.data.target === 'Open' && condition.code === 'BR-ST-02' && message.includes('acknowledge')); }
}
