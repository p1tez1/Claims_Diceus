import { convertToParamMap, provideRouter } from '@angular/router';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { ClaimListItem, Page, User } from '../../core/models/api.models';
import { ClaimsApiService } from '../../core/services/claims-api.service';
import { ReferenceApiService } from '../../core/services/reference-api.service';
import { UsersApiService } from '../../core/services/users-api.service';
import { ClaimsListComponent } from './claims-list.component';

describe('ClaimsListComponent', () => {
  let fixture: ComponentFixture<ClaimsListComponent>;
  let component: ClaimsListComponent;
  let queryParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let claims: jasmine.SpyObj<ClaimsApiService>;
  let router: jasmine.SpyObj<Router>;
  let users: BehaviorSubject<User[]>;
  const row: ClaimListItem = { id: 'claim-1', claimNumber: 'CLM-001', policyNumber: 'POL-001', clientName: 'Ada Lovelace', lossDate: '2026-01-02', causeOfLossCode: 'FIRE', status: 'Open', totalReserves: 1250 };

  beforeEach(async () => {
    queryParams = new BehaviorSubject(convertToParamMap({ page: '1', pageSize: '20' }));
    claims = jasmine.createSpyObj<ClaimsApiService>('ClaimsApiService', ['list']);
    claims.list.and.returnValue(of({ items: [row], totalCount: 1, page: 1, pageSize: 20 }));
    users = new BehaviorSubject<User[]>([{ id: 'handler-1', name: 'Case Handler', role: 'Handler' }]);
    await TestBed.configureTestingModule({ imports: [ClaimsListComponent], providers: [provideNoopAnimations(), provideRouter([]), { provide: ClaimsApiService, useValue: claims }, { provide: ReferenceApiService, useValue: { causes: () => of([{ code: 'FIRE', name: 'Fire' }]) } }, { provide: UsersApiService, useValue: { list: () => users.asObservable() } }, { provide: ActivatedRoute, useValue: { queryParamMap: queryParams.asObservable() } }] }).compileComponents();
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>; spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ClaimsListComponent); component = fixture.componentInstance; fixture.detectChanges();
  });

  it('renders claim rows with a colour-coded status badge', () => {
    expect(fixture.nativeElement.textContent).toContain('CLM-001');
    expect(fixture.nativeElement.querySelector('.status-open')).not.toBeNull();
  });

  it('requests the first page from the query parameters and changes page through the URL', () => {
    expect(claims.list).toHaveBeenCalledWith(jasmine.objectContaining({ page: 1, pageSize: 20 }));
    component.changePageSize(50);
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: jasmine.objectContaining({ page: 1, pageSize: 50 }) }));
  });

  it('builds filter query parameters including the selected handler id', () => {
    component.filters.patchValue({ status: ['Open'], dateFrom: '2026-01-01', dateTo: '2026-01-31', causeOfLossCode: 'FIRE', search: 'Ada' });
    component.selectHandler({ id: 'handler-1', name: 'Case Handler', role: 'Handler' });
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: jasmine.objectContaining({ status: ['Open'], dateFrom: '2026-01-01', dateTo: '2026-01-31', assignedHandlerId: 'handler-1', causeOfLossCode: 'FIRE', search: 'Ada' }) }));
  });

  it('shows the friendly empty state when the service returns no claims', () => {
    component.rows.set([]); component.loading.set(false); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No claims found');
  });

  it('navigates to the selected claim when a row is clicked', () => {
    component.openClaim(row);
    expect(router.navigate).toHaveBeenCalledWith(['/claims', 'claim-1']);
  });

  it('F09 sends the entire selected UTC end day, retaining date-only URL controls', () => {
    queryParams.next(convertToParamMap({ dateFrom: '2026-01-01', dateTo: '2026-01-31' }));
    const sent = claims.list.calls.mostRecent().args[0]!;
    expect(sent.dateFrom).toBe('2026-01-01T00:00:00.000Z');
    expect(sent.dateTo).toBe('2026-01-31T23:59:59.9999999Z');
    expect(component.filters.controls.dateTo.value).toBe('2026-01-31');
  });

  it('F10 recomputes assignee options as the search text changes', () => {
    expect(component.filteredUsers().length).toBe(1);
    component.filters.controls.assignedHandler.setValue('no match');
    expect(component.filteredUsers()).toEqual([]);
    component.filters.controls.assignedHandler.setValue('case');
    expect(component.filteredUsers().length).toBe(1);
  });

  it('F10 retains a URL assignee before users arrive and restores its display later', fakeAsync(() => {
    users.next([]);
    queryParams.next(convertToParamMap({ assignedHandlerId: 'handler-1' }));
    component.filters.controls.search.setValue('Ada'); tick(300);
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: jasmine.objectContaining({ assignedHandlerId: 'handler-1' }) }));
    users.next([{ id: 'handler-1', name: 'Case Handler', role: 'Handler' }]);
    expect(component.filters.controls.assignedHandler.value).toBe('Case Handler');
  }));

  it('F11 cancels older requests and clears rows on failure without terminating future loads', () => {
    const older = new Subject<Page<ClaimListItem>>(), newer = new Subject<Page<ClaimListItem>>();
    claims.list.and.returnValues(older, newer);
    queryParams.next(convertToParamMap({ search: 'old' }));
    queryParams.next(convertToParamMap({ search: 'new' }));
    newer.next({ items: [{ ...row, id: 'new' }], totalCount: 1, page: 1, pageSize: 20 });
    older.next({ items: [row], totalCount: 9, page: 1, pageSize: 20 });
    expect(component.rows()[0].id).toBe('new');
    newer.error(new Error('Network unavailable'));
    expect(component.rows()).toEqual([]);
    expect(component.totalCount()).toBe(0);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Claims could not be loaded');
    claims.list.and.returnValue(of({ items: [row], totalCount: 1, page: 1, pageSize: 20 }));
    queryParams.next(convertToParamMap({ search: 'retry' }));
    expect(component.rows()).toEqual([row]);
  });
});
