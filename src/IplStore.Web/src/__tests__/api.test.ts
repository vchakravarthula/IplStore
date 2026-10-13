import { ApiError, api, setAccessToken, shouldRotateIdempotencyKey } from '../lib/api';
import { json, mockApi } from '../test/utils';

describe('api client', () => {
  it('sends the bearer token when authenticated calls are made', async () => {
    const { calls } = mockApi({ 'GET /api/cart': { body: { items: [], total: 0 } } });
    setAccessToken('abc');

    await api('/api/cart');

    expect(calls[0].headers.Authorization).toBe('Bearer abc');
  });

  it('never sends the token for public calls (keeps catalog responses cacheable)', async () => {
    const { calls } = mockApi({ 'GET /api/products': { body: {} } });
    setAccessToken('abc');

    await api('/api/products', { auth: false });

    expect(calls[0].headers.Authorization).toBeUndefined();
  });

  it('turns problem+json into an ApiError that carries the trace id', async () => {
    mockApi({ 'GET /api/orders': { status: 409, body: { title: 'Insufficient stock', detail: 'Only 1 left', traceId: 't-9' } } });

    const error = await api('/api/orders').catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 409, title: 'Insufficient stock', message: 'Only 1 left', traceId: 't-9' });
  });

  it('survives an error response that is not JSON', async () => {
    mockApi({ 'GET /api/x': () => new Response('<html>Bad gateway</html>', { status: 502 }) });

    const error = await api('/api/x').catch((e) => e);

    expect(error).toMatchObject({ status: 502 });
  });

  it('returns undefined for 204 responses', async () => {
    mockApi({ 'DELETE /api/cart': () => json(204) });
    expect(await api('/api/cart', { method: 'DELETE' })).toBeUndefined();
  });

  it('calls the unauthorized handler on 401 so the session is dropped', async () => {
    const { setUnauthorizedHandler } = await import('../lib/api');
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    setAccessToken('expired');
    mockApi({ 'GET /api/cart': { status: 401, body: { title: 'Unauthorized' } } });

    await api('/api/cart').catch(() => undefined);

    expect(handler).toHaveBeenCalledOnce();
  });
});

describe('idempotency key rotation', () => {
  it.each([
    [new ApiError(409, 't', 'd'), true],   // definitive: nothing was created
    [new ApiError(400, 't', 'd'), true],
    [new ApiError(502, 't', 'd'), true],   // gateway failed: the order was marked Failed under this key
    [new ApiError(500, 't', 'd'), false],  // unknown outcome: keep the key so a retry replays safely
    [new ApiError(503, 't', 'd'), false],
    [new TypeError('Failed to fetch'), false]  // network error
  ])('%o -> rotate: %s', (error, expected) => {
    expect(shouldRotateIdempotencyKey(error)).toBe(expected);
  });
});
