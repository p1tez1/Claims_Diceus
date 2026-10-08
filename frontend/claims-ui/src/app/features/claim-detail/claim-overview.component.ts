import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatOption } from '@angular/material/core';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSelect } from '@angular/material/select';
import { ClaimDetail, LiabilityDetermination } from '../../core/models/api.models';

@Component({
  selector: 'app-claim-overview', standalone: true,
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, MatButton, MatOption, MatFormField, MatLabel, MatInput, MatSelect],
  template: `
    <div class="overview-grid">
      @if (claim.slaBreachedAt) { <div class="sla-alert wide"><strong>SLA breached</strong><span>This claim was flagged on {{ claim.slaBreachedAt | date:'medium' }}.</span></div> }
      <section class="panel"><div class="section-title"><h2>Loss event</h2><span class="severity" [class]="'severity '+claim.severity.toLowerCase()">{{ claim.severity }}</span></div>
        <dl><div><dt>Loss date</dt><dd>{{ claim.lossEvent.lossDate | date:'medium' }}</dd></div><div><dt>Report date</dt><dd>{{ claim.lossEvent.reportDate | date:'medium' }}</dd></div><div><dt>Location</dt><dd>{{ claim.lossEvent.lossLocation || '—' }}</dd></div><div><dt>Cause</dt><dd>{{ claim.lossEvent.causeOfLossCode }}</dd></div><div><dt>Estimated loss</dt><dd>{{ claim.lossEvent.estimatedLossAmount == null ? '—' : (claim.lossEvent.estimatedLossAmount | currency) }}</dd></div><div class="wide"><dt>Description</dt><dd>{{ claim.lossEvent.lossDescription }}</dd></div></dl>
      </section>
      <section class="panel"><h2>Liability determination</h2><p class="hint">Required before moving an investigated claim to Pending payment.</p><mat-form-field appearance="outline"><mat-label>Determination</mat-label><mat-select [formControl]="liability"><mat-option value="Accepted">Accepted</mat-option><mat-option value="PartiallyAccepted">Partially accepted</mat-option><mat-option value="Denied">Denied</mat-option></mat-select></mat-form-field><button mat-stroked-button color="primary" [disabled]="pending || claim.status === 'Closed' || claim.status === 'Withdrawn' || liability.invalid || liability.value === claim.liabilityDetermination" (click)="saveLiability()">Save determination</button>@if (claim.liabilityDeterminedAt) { <small>Last determined {{ claim.liabilityDeterminedAt | date:'medium' }}</small> }</section>
      <section class="panel wide"><h2>Claim notes</h2><mat-form-field appearance="outline"><mat-label>Notes</mat-label><textarea matInput rows="5" maxlength="4000" [formControl]="notes"></textarea></mat-form-field><div class="form-actions"><span>{{ notes.value.length }}/4000</span><button mat-flat-button color="primary" [disabled]="pending || notes.invalid || notes.value === (claim.notes || '')" (click)="notesSaved.emit(notes.value.trim() || undefined)">Save notes</button></div></section>
      <section class="panel wide"><h2>Validation issues</h2>@if (!claim.validationIssues.length) { <p class="hint">No validation issues recorded.</p> } @else { <div class="issues">@for (issue of claim.validationIssues; track issue.id) { <article><span class="issue-chip" [class.critical]="issue.severity === 'Critical'">{{ issue.severity }}</span><div><strong>{{ issue.code }}</strong><p>{{ issue.message }}</p></div><span class="issue-status">{{ issue.status }}</span></article> }</div> }</section>
    </div>`,
  styles: [`
    .overview-grid{display:grid;grid-template-columns:2fr 1fr;gap:1rem}.wide{grid-column:1/-1}.panel{border:1px solid var(--claims-border);border-radius:.75rem;padding:1.1rem;background:#fff}.panel h2{font-size:1.05rem;margin:0 0 1rem}.section-title,.form-actions,.issues article{display:flex;align-items:center}.section-title,.form-actions{justify-content:space-between}.panel dl{display:grid;grid-template-columns:repeat(2,1fr);gap:1rem;margin:0}.panel dt{font-size:.75rem;color:var(--claims-text-muted);text-transform:uppercase;font-weight:700}.panel dd{margin:.25rem 0 0}.panel mat-form-field{display:block;width:100%}.panel small{display:block;color:var(--claims-text-muted);margin-top:.75rem}.hint{color:var(--claims-text-muted)}.severity,.issue-chip{border-radius:999px;padding:.25rem .65rem;font-size:.75rem;font-weight:800;background:#e0f2fe;color:#075985}.severity.critical,.issue-chip.critical{background:#fee2e2;color:#991b1b}.severity.catastrophic{background:#f3e8ff;color:#6b21a8}.severity.minor{background:#dcfce7;color:#166534}.sla-alert{display:flex;gap:.75rem;padding:.85rem 1rem;border-radius:.65rem;background:#fff7ed;color:#9a3412}.issues{display:grid;gap:.65rem}.issues article{gap:.75rem;padding:.75rem;background:#f8fafc;border-radius:.5rem}.issues p{margin:.2rem 0}.issue-status{margin-left:auto;color:var(--claims-text-muted)}@media(max-width:800px){.overview-grid{grid-template-columns:1fr}.panel dl{grid-template-columns:1fr}}
  `], changeDetection: ChangeDetectionStrategy.OnPush
})
export class ClaimOverviewComponent {
  private currentClaim?: ClaimDetail;
  @Input({ required: true }) set claim(value: ClaimDetail) { this.currentClaim = value; this.notes.setValue(value.notes ?? '', { emitEvent: false }); this.liability.setValue((value.liabilityDetermination as LiabilityDetermination | undefined) ?? null, { emitEvent: false }); }
  get claim(): ClaimDetail { return this.currentClaim!; }
  @Input() pending = false;
  @Output() readonly notesSaved = new EventEmitter<string | undefined>();
  @Output() readonly liabilitySaved = new EventEmitter<LiabilityDetermination>();
  readonly notes = new FormControl('', { nonNullable: true, validators: Validators.maxLength(4000) });
  readonly liability = new FormControl<LiabilityDetermination | null>(null, Validators.required);
  saveLiability(): void { if (this.liability.value) this.liabilitySaved.emit(this.liability.value); }
}
