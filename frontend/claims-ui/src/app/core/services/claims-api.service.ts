import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ClaimDetail, ClaimListFilters, ClaimListItem, ClaimStatus, ClaimValidation, CreateClaimRequest, LiabilityDetermination, Page, Party, PartyInput, TransitionOptions, TransitionPreflight } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ClaimsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/claims`;

  list(filters: ClaimListFilters = {}): Observable<Page<ClaimListItem>> {
    let params = new HttpParams().set('page', filters.page ?? 1).set('pageSize', filters.pageSize ?? 20);
    for (const status of filters.status ?? []) params = params.append('status', status);
    for (const key of ['dateFrom', 'dateTo', 'assignedHandlerId', 'causeOfLossCode', 'policyId', 'search'] as const) {
      const value = filters[key];
      if (value !== undefined && value !== '') params = params.set(key, value);
    }
    return this.http.get<Page<ClaimListItem>>(this.baseUrl, { params });
  }

  get(id: string): Observable<ClaimDetail> { return this.http.get<ClaimDetail>(`${this.baseUrl}/${id}`); }
  create(request: CreateClaimRequest, idempotencyKey?: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(this.baseUrl, request, idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {});
  }
  validate(request: CreateClaimRequest): Observable<ClaimValidation> { return this.http.post<ClaimValidation>(`${this.baseUrl}/validate`, request); }
  addParty(id: string, party: PartyInput, idempotencyKey?: string): Observable<Party> { return this.http.post<Party>(`${this.baseUrl}/${id}/parties`, party, idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {}); }
  removeParty(id: string, partyId: string): Observable<void> { return this.http.delete<void>(`${this.baseUrl}/${id}/parties/${partyId}`); }
  updateNotes(id: string, notes?: string): Observable<void> { return this.http.put<void>(`${this.baseUrl}/${id}/notes`, { notes }); }
  linkPolicy(id: string, policyId: string): Observable<void> { return this.http.put<void>(`${this.baseUrl}/${id}/policy`, { policyId }); }
  assignHandler(id: string, assignedHandlerId: string): Observable<void> { return this.http.put<void>(`${this.baseUrl}/${id}/assignee`, { assignedHandlerId }); }
  updateLiability(id: string, determination: LiabilityDetermination, notes?: string): Observable<void> { return this.http.put<void>(`${this.baseUrl}/${id}/liability`, { determination, notes }); }
  transition(id: string, targetStatus: ClaimStatus, options: TransitionOptions = {}): Observable<{ status: ClaimStatus }> {
    return this.http.put<{ status: ClaimStatus }>(`${this.baseUrl}/${id}/status`, { targetStatus, reason: options.reason, acknowledgeWarnings: options.acknowledgeWarnings ?? false, closureJustification: options.closureJustification });
  }
  transitionPreflight(id: string, targetStatus: ClaimStatus, options: TransitionOptions = {}): Observable<TransitionPreflight> {
    let params = new HttpParams().set('acknowledgeWarnings', options.acknowledgeWarnings ?? false);
    if (options.reason) params = params.set('reason', options.reason);
    if (options.closureJustification) params = params.set('closureJustification', options.closureJustification);
    return this.http.get<TransitionPreflight>(`${this.baseUrl}/${id}/transitions/${targetStatus}/preflight`, { params });
  }
}
