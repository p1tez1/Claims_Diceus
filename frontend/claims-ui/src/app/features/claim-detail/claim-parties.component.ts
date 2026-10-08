import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatTooltip } from '@angular/material/tooltip';
import { Party, PartyInput } from '../../core/models/api.models';
import { PartyFormComponent } from '../../shared/party-form/party-form.component';

@Component({ selector: 'app-claim-parties', standalone: true, imports: [MatButton, MatTooltip, PartyFormComponent], template: `
  <div class="parties-heading"><div><h2>Claim parties</h2><p>People and organisations connected to this claim.</p></div><button mat-stroked-button type="button" [disabled]="pending" (click)="formOpen.set(!formOpen())">{{ formOpen() ? 'Cancel' : 'Add Party' }}</button></div>
  @if (formOpen()) { <app-party-form [pending]="pending" (partyAdded)="add($event)" /> }
  <div class="party-list">@for (party of parties; track party.id) { <article [id]="'party-'+party.id" [class.inactive]="!party.isActive"><span class="role">{{ party.role }}</span><div class="identity"><strong>{{ name(party) }}</strong><span>{{ party.type }}</span></div><div class="contact"><span>{{ party.email || 'No email' }}</span><span>{{ party.phone || 'No phone' }}</span></div><span class="state">{{ party.isActive ? 'Active' : 'Inactive' }}</span>@if (party.isActive) { <button mat-button color="warn" [disabled]="pending || isLastClaimant(party)" [matTooltip]="isLastClaimant(party) ? 'The last active Claimant cannot be removed.' : 'Remove party'" (click)="partyRemoved.emit(party.id)">Remove</button> }</article> } @empty { <p class="empty">No parties recorded.</p> }</div>
  `, styles: [`
    .parties-heading,.party-list article{display:flex;align-items:center}.parties-heading{justify-content:space-between}.parties-heading h2{margin:0}.parties-heading p{color:var(--claims-text-muted);margin:.25rem 0 1rem}.party-list{display:grid;gap:.65rem}.party-list article{gap:1rem;padding:.9rem 1rem;border:1px solid var(--claims-border);border-radius:.65rem;background:#fff}.party-list article.inactive{opacity:.58}.role{min-width:6.5rem;text-align:center;border-radius:999px;padding:.25rem .65rem;background:#e6f2f4;color:var(--claims-primary-dark);font-size:.75rem;font-weight:800}.identity{min-width:13rem}.identity span,.contact span{display:block;color:var(--claims-text-muted);font-size:.85rem}.contact{flex:1}.state{font-weight:700;color:#166534}.inactive .state{color:#64748b}.empty{padding:2rem;text-align:center;color:var(--claims-text-muted)}@media(max-width:700px){.party-list article{align-items:flex-start;flex-direction:column}.contact{width:100%}}
  `], changeDetection: ChangeDetectionStrategy.OnPush })
export class ClaimPartiesComponent {
  @Input() parties: Party[] = [];
  @Input() pending = false;
  @Input() focusId: string | null = null;
  ngOnChanges(): void { if (this.focusId) setTimeout(() => document.getElementById(this.focusId!)?.scrollIntoView({ block: 'center' }), 50); }
  @Output() readonly partyAdded = new EventEmitter<PartyInput>();
  @Output() readonly partyRemoved = new EventEmitter<string>();
  readonly formOpen = signal(false);
  name(party: Party): string { return party.type === 'Company' ? party.companyName ?? 'Unnamed company' : `${party.firstName ?? ''} ${party.lastName ?? ''}`.trim() || 'Unnamed person'; }
  isLastClaimant(party: Party): boolean { return party.role === 'Claimant' && party.isActive && this.parties.filter(item => item.role === 'Claimant' && item.isActive).length === 1; }
  add(party: PartyInput): void { if (this.pending) return; this.partyAdded.emit(party); this.formOpen.set(false); }
}
