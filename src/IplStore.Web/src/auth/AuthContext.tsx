import { useQueryClient } from '@tanstack/react-query';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, setAccessToken, setUnauthorizedHandler } from '../lib/api';
import type { TokenResponse } from '../lib/types';

interface Session {
  token: string;
  username: string;
  role: string;
}

interface AuthContextValue {
  session: Session | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (username: string) => Promise<void>;
  logout: () => void;
}

const STORAGE_KEY = 'iplstore.session';
const AuthContext = createContext<AuthContextValue | null>(null);

// sessionStorage keeps the demo login across a page refresh (cleared when the tab closes).
// Production: use an httpOnly cookie via a BFF, so scripts can never read the token.
function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();

  // Set the token synchronously during the first render so child queries never run without it.
  const [session, setSession] = useState<Session | null>(() => {
    const stored = loadSession();
    setAccessToken(stored?.token ?? null);
    return stored;
  });

  const login = useCallback(async (username: string) => {
    const response = await api<TokenResponse>('/api/auth/token', { method: 'POST', body: { username }, auth: false });
    const next: Session = { token: response.accessToken, username, role: response.role };
    setAccessToken(next.token);
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    setSession(next);
  }, []);

  const logout = useCallback(() => {
    setAccessToken(null);
    sessionStorage.removeItem(STORAGE_KEY);
    setSession(null);
    queryClient.clear();   // never show the previous user's cart or orders to the next user
  }, [queryClient]);

  useEffect(() => {
    setUnauthorizedHandler(logout);
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  const value = useMemo<AuthContextValue>(
    () => ({ session, isAuthenticated: session !== null, isAdmin: session?.role === 'Admin', login, logout }),
    [session, login, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.');
  return context;
}
