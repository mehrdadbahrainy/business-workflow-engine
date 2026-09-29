import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([(request, next) => {
      const subject = localStorage.getItem('demo-subject') ?? 'requester@example.test';
      const accessToken = sessionStorage.getItem('api-access-token');
      const headers: Record<string, string> = { 'X-Demo-User': subject };
      if (accessToken) headers['Authorization'] = `Bearer ${accessToken}`;
      return next(request.clone({ setHeaders: headers }));
    }]))
  ]
};
