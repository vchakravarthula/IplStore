import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CartPage } from '../pages/CartPage';
import { cartWith, emptyCart, jersey, mockApi, problem, renderApp, signedInSession } from '../test/utils';

describe('cart page', () => {
  beforeEach(() => signedInSession());

  it('shows lines, line totals and the order total', async () => {
    mockApi({ 'GET /api/cart': { body: cartWith(2) } });
    renderApp(<CartPage />, { route: '/cart', path: '/cart', guarded: true });

    expect(await screen.findByText(jersey.name)).toBeInTheDocument();
    expect(screen.getAllByText('₹3,998.00')).not.toHaveLength(0);
    expect(screen.getByRole('link', { name: /proceed to checkout/i })).toHaveAttribute('href', '/checkout');
  });

  it('shows an empty state with a way back to the catalog', async () => {
    mockApi({ 'GET /api/cart': { body: emptyCart } });
    renderApp(<CartPage />, { route: '/cart', path: '/cart', guarded: true });

    expect(await screen.findByText(/your cart is empty/i)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /proceed to checkout/i })).not.toBeInTheDocument();
  });

  it('changes the quantity with PUT and re-renders from the response', async () => {
    const { callsTo } = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'PUT /api/cart/items/p-jersey': { body: cartWith(5) }
    });
    renderApp(<CartPage />, { route: '/cart', path: '/cart', guarded: true });

    await userEvent.selectOptions(await screen.findByLabelText(`Quantity for ${jersey.name}`), '5');

    await waitFor(() => expect(callsTo('PUT', '/api/cart/items/p-jersey')).toHaveLength(1));
    expect(callsTo('PUT', '/api/cart/items/p-jersey')[0].body).toEqual({ quantity: 5 });
    expect(await screen.findAllByText('₹9,995.00')).not.toHaveLength(0);
  });

  it('removes a line with DELETE and then shows the empty state', async () => {
    const { callsTo } = mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'DELETE /api/cart/items/p-jersey': { body: emptyCart }
    });
    renderApp(<CartPage />, { route: '/cart', path: '/cart', guarded: true });

    const row = (await screen.findByText(jersey.name)).closest('tr')!;
    await userEvent.click(within(row).getByRole('button', { name: /remove/i }));

    await waitFor(() => expect(callsTo('DELETE', '/api/cart/items/p-jersey')).toHaveLength(1));
    expect(await screen.findByText(/your cart is empty/i)).toBeInTheDocument();
  });

  it('shows the server message (with trace id) when an update is rejected', async () => {
    mockApi({
      'GET /api/cart': { body: cartWith(2) },
      'PUT /api/cart/items/p-jersey': problem(409, 'Insufficient stock', 'Requested: 9, available: 4', 'trace-77')
    });
    renderApp(<CartPage />, { route: '/cart', path: '/cart', guarded: true });

    await userEvent.selectOptions(await screen.findByLabelText(`Quantity for ${jersey.name}`), '9');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Requested: 9, available: 4');
    expect(alert).toHaveTextContent('trace-77');
  });

  it('shows the item count in the header', async () => {
    mockApi({ 'GET /api/cart': { body: cartWith(3) } });
    renderApp(<CartPage />, { route: '/cart', path: '/cart', guarded: true });

    expect(await screen.findByLabelText('items in cart')).toHaveTextContent('3');
  });
});
