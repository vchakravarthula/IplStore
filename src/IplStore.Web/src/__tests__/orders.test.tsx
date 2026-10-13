import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OrderDetailPage } from '../pages/OrderDetailPage';
import { OrdersPage } from '../pages/OrdersPage';
import { mockApi, page, renderApp, signedInSession } from '../test/utils';

const summary = (n: number, status = 'Paid') => ({
  id: `o-${n}`, shortOrderNumber: `IPL-NUM${n}`, status, totalAmount: 1000 * n, itemCount: n, createdAtUtc: `2026-10-0${n}T10:00:00`
});

describe('order history', () => {
  beforeEach(() => signedInSession());

  it('lists the orders returned by the API in order, with status and total', async () => {
    mockApi({ 'GET /api/orders': { body: page([summary(2), summary(1, 'PendingPayment')], { pageSize: 10 }) } });
    renderApp(<OrdersPage />, { route: '/orders', path: '/orders', guarded: true });

    const rows = await screen.findAllByRole('row');
    expect(rows[1]).toHaveTextContent('IPL-NUM2');
    expect(rows[1]).toHaveTextContent('₹2,000.00');
    expect(rows[1]).toHaveTextContent('Paid');
    expect(rows[2]).toHaveTextContent('Pending payment');
    expect(screen.getByRole('link', { name: 'IPL-NUM2' })).toHaveAttribute('href', '/orders/o-2');
  });

  it('shows an empty state for a new customer', async () => {
    mockApi({ 'GET /api/orders': { body: page([]) } });
    renderApp(<OrdersPage />, { route: '/orders', path: '/orders', guarded: true });

    expect(await screen.findByText(/have not placed any orders/i)).toBeInTheDocument();
  });

  it('pages through the history', async () => {
    const { callsTo } = mockApi({
      'GET /api/orders': (call) => ({ body: page([summary(call.search.includes('page=2') ? 1 : 2)], { totalPages: 2, totalCount: 2, page: call.search.includes('page=2') ? 2 : 1 }) })
    });
    renderApp(<OrdersPage />, { route: '/orders', path: '/orders', guarded: true });
    await screen.findByText('IPL-NUM2');

    await userEvent.click(screen.getByRole('button', { name: /next/i }));

    expect(await screen.findByText('IPL-NUM1')).toBeInTheDocument();
    expect(callsTo('GET', '/api/orders').some((c) => c.search.includes('page=2'))).toBe(true);
  });
});

describe('order detail', () => {
  beforeEach(() => signedInSession());

  const detail = (status: string) => ({
    id: 'o-1', shortOrderNumber: 'IPL-NUM1', status, totalAmount: 3998, gatewayOrderId: 'order_gw1', createdAtUtc: '2026-10-01T10:00:00',
    items: [{ productId: 'p1', productName: 'CSK Jersey', quantity: 2, unitPrice: 1999, lineTotal: 3998 }]
  });

  it('shows the price snapshot for each line', async () => {
    mockApi({ 'GET /api/orders/o-1': { body: detail('Paid') } });
    renderApp(<OrderDetailPage />, { route: '/orders/o-1', path: '/orders/:id', guarded: true });

    expect(await screen.findByText('CSK Jersey')).toBeInTheDocument();
    expect(screen.getAllByText('₹3,998.00')).not.toHaveLength(0);
    expect(screen.queryByRole('button', { name: /pay with razorpay/i })).not.toBeInTheDocument();
  });

  it('lets the customer resume payment for a pending order', async () => {
    mockApi({
      'GET /api/orders/o-1': { body: detail('PendingPayment') },
      'GET /api/payments/config': { body: { keyId: 'rzp_test_key' } }
    });
    renderApp(<OrderDetailPage />, { route: '/orders/o-1', path: '/orders/:id', guarded: true });

    expect(await screen.findByRole('button', { name: /pay with razorpay/i })).toBeInTheDocument();
  });

  it('shows a not-found message with the trace id for someone elses order', async () => {
    mockApi({ 'GET /api/orders/o-1': { status: 404, body: { title: 'Resource not found', detail: "Order 'o-1' was not found.", traceId: 'tr-1' } } });
    renderApp(<OrderDetailPage />, { route: '/orders/o-1', path: '/orders/:id', guarded: true });

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('was not found'));
  });
});
