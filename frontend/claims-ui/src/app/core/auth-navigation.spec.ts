import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app.routes';

let constructed = 0;
@Component({ standalone: true, template: '' })
class ProtectedFeature { constructor() { constructed++; } }

describe('F03 protected navigation', () => {
  beforeEach(() => {
    sessionStorage.clear(); constructed = 0;
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      provideRouter(routes.filter(route => route.loadComponent).map(({ loadComponent, ...route }) => ({ ...route, component: ProtectedFeature })))
    ] });
  });
  for (const url of ['/claims', '/claims/new', '/claims/claim-id']) {
    it(`waits for the delayed login before constructing ${url}`, async () => {
      const harness = await RouterTestingHarness.create();
      const navigation = harness.navigateByUrl(url);
      await new Promise(resolve => setTimeout(resolve, 0));
      expect(constructed).toBe(0);
      const http = TestBed.inject(HttpTestingController);
      http.expectOne('/api/auth/dev-login').flush({ accessToken: 'jwt', user: { id: 'handler', role: 'Handler', name: 'Handler' } });
      await navigation;
      expect(constructed).toBe(1); http.verify();
    });
  }
});
