import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { RazorpayOptions } from '../lib/razorpay';
import { CheckoutPage } from '../pages/CheckoutPage';
import { cartWith, emptyCart, mockApi, problem, renderApp, signedInSession } from '../test/utils';

const placed = {
  orderId: 'o-1', shortOrderNumber: 'IPL-ABC123', totalAmount: 3998, currency: 'INR', status: 'PendingPayment', gatewayOrderId: 'order_gw1'
};

const open = () => renderApp(<CheckoutPage />, { route: '/checkout', path: '/checkout', guarded: true });

describe('checkout', () => {
  beforeEach(() => signedInSession());

  it('reviews the cart and places the order with an idempotency key', async () => {
    const { callsTo } = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'POST /api/orders/checkout': { body: placed }
    });
    open();

    await userEvent.click(await screen.findByRole('button', { name: /place order/i }));

    expect(await screen.findByText(/order IPL-ABC123 placed/i)).toBeInTheDocument();
    const call = callsTo('POST', '/api/orders/checkout')[0];
    expect(call.headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/);
    expect(call.body).toBeUndefined();   // the server decides items and prices; the client sends nothing about them
  });

  it('reuses the SAME key when retrying after a server/network failure (so no duplicate order can be created)', async () => {
    let attempt = 0;
    const { callsTo } = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'POST /api/orders/checkout': () => (++attempt === 1 ? problem(503, 'Service temporarily unavailable', 'Cart store is unavailable.') : { body: placed })
    });
    open();

    await userEvent.click(await screen.findByRole('button', { name: /place order/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Cart store is unavailable.');
    await userEvent.click(screen.getByRole('button', { name: /place order/i }));

    await screen.findByText(/order IPL-ABC123 placed/i);
    const [first, second] = callsTo('POST', '/api/orders/checkout');
    expect(second.headers['Idempotency-Key']).toBe(first.headers['Idempotency-Key']);
  });

  it('uses a NEW key after a definitive rejection such as insufficient stock', async () => {
    let attempt = 0;
    const { callsTo } = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'POST /api/orders/checkout': () => (++attempt === 1 ? problem(409, 'Insufficient stock', 'Requested: 2, available: 1') : { body: placed })
    });
    open();

    await userEvent.click(await screen.findByRole('button', { name: /place order/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('available: 1');
    await userEvent.click(screen.getByRole('button', { name: /place order/i }));

    await screen.findByText(/order IPL-ABC123 placed/i);
    const [first, second] = callsTo('POST', '/api/orders/checkout');
    expect(second.headers['Idempotency-Key']).not.toBe(first.headers['Idempotency-Key']);
  });

  it('asks the user to retry when the payment provider failed, and starts a fresh attempt', async () => {
    const { callsTo } = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'POST /api/orders/checkout': { body: { ...placed, status: 'Failed', gatewayOrderId: null } }
    });
    open();

    await userEvent.click(await screen.findByRole('button', { name: /place order/i }));
    expect(await screen.findByText(/could not create the order/i)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /place order/i }));

    await waitFor(() => expect(callsTo('POST', '/api/orders/checkout')).toHaveLength(2));
    const [first, second] = callsTo('POST', '/api/orders/checkout');
    expect(second.headers['Idempotency-Key']).not.toBe(first.headers['Idempotency-Key']);
  });

  it('shows an empty-cart message instead of a checkout button', async () => {
    mockApi({ 'GET /api/cart': { body: emptyCart } });
    open();

    expect(await screen.findByText(/your cart is empty/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /place order/i })).not.toBeInTheDocument();
  });
});

describe('payment', () => {
  beforeEach(() => signedInSession());

  async function placeOrder(extraRoutes = {}) {
    const mock = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'POST /api/orders/checkout': { body: placed },
      'GET /api/payments/config': { body: { keyId: 'rzp_test_key' } },
      ...extraRoutes
    });
    open();
    await userEvent.click(await screen.findByRole('button', { name: /place order/i }));
    await screen.findByRole('button', { name: /pay with razorpay/i });
    return mock;
  }

  it('opens Razorpay with the order, amount in paise and the public key, then confirms the signed result with the API', async () => {
    let options!: RazorpayOptions;
    const open = vi.fn();
    window.Razorpay = class {
      constructor(o: RazorpayOptions) { options = o; }
      open = open;
      on = vi.fn();
    } as unknown as typeof window.Razorpay;
    const { callsTo } = await placeOrder({ 'POST /api/orders/o-1/confirm-payment': { body: { orderId: 'o-1', status: 'Paid' } } });

    await userEvent.click(screen.getByRole('button', { name: /pay with razorpay/i }));

    await waitFor(() => expect(open).toHaveBeenCalled());
    expect(options).toMatchObject({ key: 'rzp_test_key', order_id: 'order_gw1', amount: 399800, currency: 'INR' });
    // Contact details are prefilled so the customer is not asked (and cannot mistype) a phone or e-mail in the popup.
    expect(options.prefill?.contact).toMatch(/^\d{10}$/);
    expect(options.prefill?.email).toContain('@');

    // Razorpay reports success with a signed proof; the UI forwards it untouched for server-side verification.
    options.handler({ razorpay_order_id: 'order_gw1', razorpay_payment_id: 'pay_123', razorpay_signature: 'sig_abc' });

    expect(await screen.findByText(/payment successful/i)).toBeInTheDocument();
    expect(callsTo('POST', '/api/orders/o-1/confirm-payment')[0].body).toEqual({
      gatewayOrderId: 'order_gw1', gatewayPaymentId: 'pay_123', signature: 'sig_abc'
    });
  });

  it('does not mark the order paid when the API rejects the signature', async () => {
    let options!: RazorpayOptions;
    window.Razorpay = class {
      constructor(o: RazorpayOptions) { options = o; }
      open = vi.fn();
      on = vi.fn();
    } as unknown as typeof window.Razorpay;
    await placeOrder({ 'POST /api/orders/o-1/confirm-payment': problem(400, 'Validation failed', 'Invalid payment signature.') });

    await userEvent.click(screen.getByRole('button', { name: /pay with razorpay/i }));
    await waitFor(() => expect(options).toBeDefined());
    options.handler({ razorpay_order_id: 'order_gw1', razorpay_payment_id: 'pay_123', razorpay_signature: 'forged' });

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid payment signature.');
    expect(screen.queryByText(/payment successful/i)).not.toBeInTheDocument();
  });

  it('tells the user their order is held when they close the payment window', async () => {
    let options!: RazorpayOptions;
    window.Razorpay = class {
      constructor(o: RazorpayOptions) { options = o; }
      open = vi.fn();
      on = vi.fn();
    } as unknown as typeof window.Razorpay;
    await placeOrder();

    await userEvent.click(screen.getByRole('button', { name: /pay with razorpay/i }));
    await waitFor(() => expect(options).toBeDefined());
    act(() => options.modal!.ondismiss!());

    expect(await screen.findByText(/payment window closed/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /pay with razorpay/i })).toBeEnabled();
  });

  it('offers the dev-only simulated payment only when enabled, and confirms through the dev endpoint', async () => {
    vi.stubEnv('VITE_ENABLE_SIMULATED_PAYMENT', 'true');
    const { callsTo } = await placeOrder({ 'POST /api/dev/orders/o-1/simulate-payment': { body: { orderId: 'o-1', status: 'Paid' } } });

    await userEvent.click(screen.getByRole('button', { name: /simulate payment/i }));

    expect(await screen.findByText(/payment successful/i)).toBeInTheDocument();
    expect(callsTo('POST', '/api/dev/orders/o-1/simulate-payment')).toHaveLength(1);
  });

  it('hides the simulated payment button when the flag is off (as in production builds)', async () => {
    vi.stubEnv('VITE_ENABLE_SIMULATED_PAYMENT', 'false');
    await placeOrder();

    expect(screen.queryByRole('button', { name: /simulate payment/i })).not.toBeInTheDocument();
  });
});
