import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ClaimsApiService } from './claims-api.service';
import { ReservesApiService } from './reserves-api.service';
import { DocumentsApiService } from './documents-api.service';
import { AuditApiService } from './audit-api.service';
import { ReferenceApiService } from './reference-api.service';
import { PoliciesApiService } from './policies-api.service';
import { UsersApiService } from './users-api.service';
import { CreateClaimRequest } from '../models/api.models';

describe('typed API services', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());

  it('builds claim list parameters and create body', () => {
    const service = TestBed.inject(ClaimsApiService);
    service.list({ page: 2, pageSize: 50, status: ['Open', 'Draft'], search: 'CLM' }).subscribe();
    const list = http.expectOne((request) => request.url === '/api/claims');
    expect(list.request.params.getAll('status')).toEqual(['Open', 'Draft']); expect(list.request.params.get('page')).toBe('2'); expect(list.request.params.get('search')).toBe('CLM');
    list.flush({ items: [], totalCount: 0, page: 2, pageSize: 50 });
    const body: CreateClaimRequest = { lossDate: '2026-01-01T00:00:00Z', lossDescription: 'A sufficiently detailed description', causeOfLossCode: 'COL', parties: [], riskObjects: [] };
    service.create(body, 'key-1').subscribe();
    const create = http.expectOne('/api/claims'); expect(create.request.method).toBe('POST'); expect(create.request.body).toEqual(body); expect(create.request.headers.get('Idempotency-Key')).toBe('key-1'); create.flush({});
  });

  it('uses reserve transaction URLs and bodies', () => {
    const service = TestBed.inject(ReservesApiService);
    service.submit('claim-1', { component: 'Indemnity', amount: 12000, changeReason: 'Initial estimate', transactionType: 'Add' }).subscribe();
    const submit = http.expectOne('/api/claims/claim-1/reserves'); expect(submit.request.body.amount).toBe(12000); submit.flush({});
    service.reject('claim-1', 'txn-1', 'Insufficient evidence').subscribe();
    const reject = http.expectOne('/api/claims/claim-1/reserves/txn-1/reject'); expect(reject.request.body).toEqual({ rejectionReason: 'Insufficient evidence' }); reject.flush(null);
  });

  it('uploads documents as multipart data', () => {
    const service = TestBed.inject(DocumentsApiService); const file = new File(['content'], 'evidence.txt', { type: 'text/plain' });
    service.upload('claim-1', file, 'Evidence', 'note').subscribe();
    const request = http.expectOne('/api/claims/claim-1/documents'); expect(request.request.method).toBe('POST'); expect(request.request.body instanceof FormData).toBeTrue(); expect((request.request.body as FormData).get('documentType')).toBe('Evidence'); request.flush({});
  });

  it('requests paged audit entries', () => {
    TestBed.inject(AuditApiService).get('claim-1', 3, 10).subscribe();
    const request = http.expectOne((candidate) => candidate.url === '/api/claims/claim-1/audit'); expect(request.request.params.get('page')).toBe('3'); expect(request.request.params.get('pageSize')).toBe('10'); request.flush({ items: [], totalCount: 0, page: 3, pageSize: 10 });
  });

  it('requests reference values with filters', () => {
    const service = TestBed.inject(ReferenceApiService); service.causes('Property').subscribe();
    const causes = http.expectOne((request) => request.url === '/api/reference/cause-of-loss-codes'); expect(causes.request.params.get('perilCategory')).toBe('Property'); causes.flush([]);
    service.claimStatuses().subscribe(); const statuses = http.expectOne('/api/reference/claim-statuses'); statuses.flush([]);
  });

  it('requests policy search and coverage URLs', () => {
    const service = TestBed.inject(PoliciesApiService); service.search('Acme').subscribe();
    const search = http.expectOne((request) => request.url === '/api/policies/search'); expect(search.request.params.get('q')).toBe('Acme'); search.flush([]);
    service.coverage('policy-1').subscribe(); const coverage = http.expectOne('/api/policies/policy-1/coverage'); coverage.flush([]);
  });

  it('requests users by exact role', () => {
    TestBed.inject(UsersApiService).list('Supervisor').subscribe();
    const request = http.expectOne((candidate) => candidate.url === '/api/reference/users'); expect(request.request.params.get('role')).toBe('Supervisor'); request.flush([]);
  });
});
