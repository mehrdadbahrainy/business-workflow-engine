import { Injectable } from '@angular/core';

type AuthMode = 'demo' | 'oidc';
interface BrowserAuthConfig {
  mode: AuthMode;
  authority: string | null;
  clientId: string | null;
  scope: string;
  registrationUrl: string | null;
}
export interface AuthenticatedUser {
  subject: string;
  displayName: string;
  roles: string[];
}
interface DiscoveryDocument {
  issuer: string;
  authorization_endpoint: string;
  token_endpoint: string;
}
interface PendingLogin {
  state: string;
  verifier: string;
  started: number;
}
const ACCESS_TOKEN_KEY = 'flowforge-access-token';
const EXPIRES_KEY = 'flowforge-token-expires-at';
const LOGIN_KEY = 'flowforge-oidc-pending';
const DEMO_SUBJECT = 'demo-subject';
const DEMO_ROLES = 'demo-roles';

function bytesToBase64Url(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
function randomToken(size = 32): string {
  return bytesToBase64Url(crypto.getRandomValues(new Uint8Array(size)));
}
function shortMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Unexpected authentication error.';
}
function requireHttps(url: string, label: string): URL {
  const parsed = new URL(url);
  if (parsed.protocol !== 'https:' && !(parsed.protocol === 'http:' && parsed.hostname === 'localhost')) {
    throw new Error(label + ' must use HTTPS.');
  }
  return parsed;
}

/**
 * Uses Authorization Code + PKCE for a public OIDC SPA client.
 * Only /auth/me (server-verified bearer token) is authoritative for identity/roles.
 * The browser never trusts a decoded ID token or a user-entered role for production authorization.
 * The identity provider is responsible for registration, account recovery and MFA.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  ready = false;
  authenticated = false;
  mode: AuthMode = 'oidc';
  profile: AuthenticatedUser | null = null;
  error = '';
  config: BrowserAuthConfig | null = null;

  get isDemo(): boolean { return this.mode === 'demo'; }
  get registrationUrl(): string | null { return this.config?.registrationUrl ?? null; }
  get configured(): boolean {
    return this.isDemo || !!(this.config?.authority && this.config.clientId);
  }
  get accessToken(): string | null {
    if (!this.authenticated || this.isDemo) return null;
    const expires = Number(sessionStorage.getItem(EXPIRES_KEY) || 0);
    return expires > Date.now() + 5_000 ? sessionStorage.getItem(ACCESS_TOKEN_KEY) : null;
  }
  get demoSubject(): string | null {
    return this.isDemo && this.authenticated ? localStorage.getItem(DEMO_SUBJECT) : null;
  }
  get demoRoles(): string { return localStorage.getItem(DEMO_ROLES) ?? ''; }

  async initialize(): Promise<void> {
    try {
      const response = await fetch('/api/v1/auth/config', { cache: 'no-store' });
      if (!response.ok) throw new Error('Could not load authentication configuration (HTTP ' + response.status + ').');
      const config = await response.json() as BrowserAuthConfig;
      if (config.mode !== 'demo' && config.mode !== 'oidc') throw new Error('Unsupported authentication mode.');
      this.config = config;
      this.mode = config.mode;
      if (this.isDemo) {
        const existing = localStorage.getItem(DEMO_SUBJECT);
        if (existing) {
          try {
            await this.verify({
              'X-Demo-User': existing, 'X-Demo-Roles': localStorage.getItem(DEMO_ROLES) ?? ''
            });
          } catch {
            localStorage.removeItem(DEMO_SUBJECT);
            localStorage.removeItem(DEMO_ROLES);
          }
        }
      } else {
        if (location.pathname === '/auth/callback') await this.completeSignIn();
        const token = sessionStorage.getItem(ACCESS_TOKEN_KEY);
        const expiration = Number(sessionStorage.getItem(EXPIRES_KEY) || 0);
        if (token && expiration > Date.now() + 5_000) {
          try { await this.verify({ Authorization: 'Bearer ' + token }); }
          catch {
            this.clearToken();
            this.error = 'Your session could not be verified. Please sign in again.';
          }
        } else {
          this.clearToken();
        }
      }
    } catch (error) {
      this.error = shortMessage(error);
    } finally {
      this.ready = true;
    }
  }

  async startDemo(subject: string, roles: string): Promise<boolean> {
    if (!this.isDemo) return false;
    const normalized = subject.trim();
    if (!normalized || normalized.length > 200) {
      this.error = 'Choose a development identity.';
      return false;
    }
    try {
      await this.verify({ 'X-Demo-User': normalized, 'X-Demo-Roles': roles.trim() });
      localStorage.setItem(DEMO_SUBJECT, normalized);
      localStorage.setItem(DEMO_ROLES, roles.trim());
      this.error = '';
      return true;
    } catch (error) {
      this.error = 'Demo sign-in failed. ' + shortMessage(error);
      return false;
    }
  }

  async beginSignIn(): Promise<void> {
    if (!this.configured || this.isDemo) {
      this.error = 'Sign-in is not configured. An administrator must set OIDC_CLIENT_ID and OIDC_AUTHORITY.';
      return;
    }
    try {
      const metadata = await this.discovery();
      const state = randomToken();
      const verifier = randomToken(48);
      const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
      const challenge = bytesToBase64Url(new Uint8Array(digest));
      const pending: PendingLogin = { state, verifier, started: Date.now() };
      sessionStorage.setItem(LOGIN_KEY, JSON.stringify(pending));
      const destination = requireHttps(metadata.authorization_endpoint, 'Authorization endpoint');
      destination.searchParams.set('client_id', this.config!.clientId!);
      destination.searchParams.set('redirect_uri', location.origin + '/auth/callback');
      destination.searchParams.set('response_type', 'code');
      destination.searchParams.set('scope', this.config!.scope || 'openid profile email');
      destination.searchParams.set('code_challenge', challenge);
      destination.searchParams.set('code_challenge_method', 'S256');
      destination.searchParams.set('state', state);
      destination.searchParams.set('nonce', randomToken());
      location.assign(destination.toString());
    } catch (error) {
      sessionStorage.removeItem(LOGIN_KEY);
      this.error = 'Unable to start sign-in. ' + shortMessage(error);
    }
  }

  logout(): void {
    this.authenticated = false;
    this.profile = null;
    this.error = '';
    this.clearToken();
    localStorage.removeItem(DEMO_SUBJECT);
    localStorage.removeItem(DEMO_ROLES);
    // Ends the local session; provider SSO session may still exist.
  }

  sessionExpired(): void {
    this.logout();
    this.error = 'Your session expired. Sign in to continue.';
  }

  private async discovery(): Promise<DiscoveryDocument> {
    const authority = this.config?.authority;
    if (!authority) throw new Error('OIDC authority is missing.');
    const issuer = requireHttps(authority, 'OIDC authority');
    const endpoint = new URL(issuer.toString().replace(/\/+$/, '') + '/.well-known/openid-configuration');
    const response = await fetch(endpoint.toString(), { cache: 'no-store' });
    if (!response.ok) throw new Error('Identity-provider discovery failed (HTTP ' + response.status + ').');
    const metadata = await response.json() as DiscoveryDocument;
    if (metadata.issuer.replace(/\/+$/, '') !== issuer.toString().replace(/\/+$/, '')) {
      throw new Error('Identity provider issuer does not match the configured authority.');
    }
    requireHttps(metadata.authorization_endpoint, 'Authorization endpoint');
    requireHttps(metadata.token_endpoint, 'Token endpoint');
    return metadata;
  }

  private async completeSignIn(): Promise<void> {
    try {
      const parameters = new URLSearchParams(location.search);
      if (parameters.has('error')) throw new Error('The identity provider declined sign-in.');
      const pendingRaw = sessionStorage.getItem(LOGIN_KEY);
      const pending = pendingRaw ? JSON.parse(pendingRaw) as PendingLogin : null;
      if (!pending || Date.now() - pending.started > 600_000 ||
          !parameters.get('state') || parameters.get('state') !== pending.state) {
        throw new Error('Login state is missing or expired. Start sign-in again.');
      }
      const code = parameters.get('code');
      if (!code) throw new Error('Identity provider did not return an authorization code.');
      const metadata = await this.discovery();
      const fields = new URLSearchParams({
        grant_type: 'authorization_code', code, client_id: this.config!.clientId!,
        redirect_uri: location.origin + '/auth/callback', code_verifier: pending.verifier
      });
      const response = await fetch(metadata.token_endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: fields
      });
      if (!response.ok) throw new Error('The identity provider rejected the code (HTTP ' + response.status + ').');
      const tokens = await response.json() as { access_token?: string; expires_in?: number; token_type?: string };
      if (!tokens.access_token || tokens.token_type?.toLowerCase() !== 'bearer') {
        throw new Error('The identity provider did not return a Bearer access token.');
      }
      sessionStorage.setItem(ACCESS_TOKEN_KEY, tokens.access_token);
      sessionStorage.setItem(EXPIRES_KEY, String(Date.now() + Math.max(0, Number(tokens.expires_in || 0)) * 1000));
    } finally {
      sessionStorage.removeItem(LOGIN_KEY);
      // Remove one-time code and state from the visible URL, even after a failed callback.
      history.replaceState(null, '', '/');
    }
  }

  private async verify(headers: Record<string, string>): Promise<void> {
    const response = await fetch('/api/v1/auth/me', { headers, cache: 'no-store' });
    if (!response.ok) throw new Error('Identity verification failed (HTTP ' + response.status + ').');
    const profile = await response.json() as AuthenticatedUser;
    if (!profile.subject) throw new Error('Identity provider did not supply a subject.');
    this.profile = profile;
    this.authenticated = true;
  }

  private clearToken(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(EXPIRES_KEY);
    sessionStorage.removeItem(LOGIN_KEY);
  }
}
