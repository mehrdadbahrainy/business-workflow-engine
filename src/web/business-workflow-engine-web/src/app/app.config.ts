import { ApplicationConfig, inject, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { AuthService } from './auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([(request, next) => {
      const auth = inject(AuthService);
      const headers: Record<string, string> = {};
      if (auth.authenticated && auth.isDemo && auth.demoSubject) {
        headers['X-Demo-User'] = auth.demoSubject;
        headers['X-Demo-Roles'] = auth.demoRoles;
      } else if (auth.authenticated && auth.accessToken) {
        headers['Authorization'] = `Bearer ${auth.accessToken}`;
      }
      return next(Object.keys(headers).length ? request.clone({ setHeaders: headers }) : request);
    }]))
  ]
};
