import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi } from 'vitest';
import { AuthProvider } from '../auth/AuthContext';
import { RequireAuth } from '../auth/RequireAuth';
import { Layout } from '../components/Layout';

// ---------- fake backend: fetch routed by "METHOD /path" ----------

export interface RecordedCall {
  method: string;
  path: string;
  search: string;
  headers: Record<string, string>;
  body: unknown;
}

type Handler = (call: RecordedCall) => { status?: number; body?: unknown } | Response;

export function mockApi(routes: Record<string, Handler | { status?: number; body?: unknown }>) {
  const calls: RecordedCall[] = [];

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost');
    const method = (init?.method ?? 'GET').toUpperCase();
    const call: RecordedCall = {
      method,
      path: url.pathname,
      search: url.search,
      headers: { ...(init?.headers as Record<string, string> | undefined) },
      body: init?.body ? JSON.parse(String(init.body)) : undefined
    };
    calls.push(call);

    const route = routes[`${method} ${url.pathname}`];
    if (!route) return json(404, { title: 'Not found', detail: `No mock for ${method} ${url.pathname}` });

    const result = typeof route === 'function' ? route(call) : route;
    if (result instanceof Response) return result;
    return json(result.status ?? 200, result.body);
  });

  vi.stubGlobal('fetch', fetchMock);
  return { calls, fetchMock, callsTo: (method: string, path: string) => calls.filter((c) => c.method === method && c.path === path) };
}

export const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' }
  });

export const problem = (status: number, title: string, detail: string, traceId = 'trace-123') => ({ status, body: { status, title, detail, traceId } });

// ---------- rendering ----------

export function signedInSession(role = 'Customer', username = 'customer') {
  sessionStorage.setItem('iplstore.session', JSON.stringify({ token: 'test-token', username, role }));
}

/** Renders `ui` under the real providers, optionally behind the real auth guard and layout. */
export function renderApp(
  ui: ReactElement,
  { route = '/', path = '/', guarded = false }: { route?: string | { pathname: string; state?: unknown }; path?: string; guarded?: boolean } = {}
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  const page = guarded ? (
    <Route element={<RequireAuth />}>
      <Route path={path} element={ui} />
    </Route>
  ) : (
    <Route path={path} element={ui} />
  );

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <AuthProvider>
          <Routes>
            <Route element={<Layout />}>
              {page}
              <Route path="/login" element={<div>LOGIN PAGE</div>} />
              <Route path="/orders/:id" element={<div>ORDER DETAIL PAGE</div>} />
              <Route path="/cart" element={<div>CART PAGE</div>} />
              <Route path="*" element={<div>HOME PAGE</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

// ---------- sample data ----------

export const jersey = {
  id: 'p-jersey', name: 'Chennai Super Kings Jersey', sku: 'CSK-JERSEY', description: 'Official jersey', price: 1999, stockQuantity: 100,
  imageUrl: '', franchiseId: 'f1', franchiseName: 'Chennai Super Kings', franchiseCode: 'CSK', categoryId: 'c1', categoryName: 'Jersey'
};

export const photo = { ...jersey, id: 'p-photo', name: 'CSK Autographed Photo', sku: 'CSK-PHOTO', price: 2999, stockQuantity: 3, categoryName: 'Autographed Photo' };

export const page = <T,>(items: T[], extra = {}) => ({ items, page: 1, pageSize: 12, totalCount: items.length, totalPages: 1, ...extra });

export const cartWith = (quantity = 2) => ({
  items: [{ productId: jersey.id, name: jersey.name, imageUrl: '', unitPrice: 1999, quantity, lineTotal: 1999 * quantity, availableStock: 100 }],
  total: 1999 * quantity
});

export const emptyCart = { items: [], total: 0 };
