import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CatalogPage } from '../pages/CatalogPage';
import { cartWith, jersey, mockApi, page, photo, renderApp, signedInSession } from '../test/utils';

describe('catalog', () => {
  it('lists products with price, franchise and a low-stock warning', async () => {
    mockApi({ 'GET /api/products': { body: page([jersey, photo]) } });
    renderApp(<CatalogPage />);

    expect(await screen.findByText(jersey.name)).toBeInTheDocument();
    expect(screen.getByText('₹1,999.00')).toBeInTheDocument();
    expect(screen.getByText('Only 3 left')).toBeInTheDocument();
  });

  it('searches after the user stops typing, without sending a token', async () => {
    signedInSession();
    const { callsTo } = mockApi({
      'GET /api/products': { body: page([jersey]) },
      'GET /api/cart': { body: { items: [], total: 0 } }
    });
    renderApp(<CatalogPage />);
    await screen.findByText(jersey.name);

    await userEvent.type(screen.getByLabelText('Search products'), 'csk');

    await waitFor(() => expect(callsTo('GET', '/api/products').some((c) => c.search.includes('search=csk'))).toBe(true));
    expect(callsTo('GET', '/api/products').every((c) => c.headers.Authorization === undefined)).toBe(true);
  });

  it('shows an empty state when nothing matches', async () => {
    mockApi({ 'GET /api/products': { body: page([]) } });
    renderApp(<CatalogPage />);

    expect(await screen.findByText(/no products match/i)).toBeInTheDocument();
  });

  it('adds to the cart sending only product id and quantity (never a price)', async () => {
    signedInSession();
    const { callsTo } = mockApi({
      'GET /api/products': { body: page([jersey]) },
      'GET /api/cart': { body: { items: [], total: 0 } },
      'POST /api/cart/items': { body: cartWith(1) }
    });
    renderApp(<CatalogPage />);

    await userEvent.click(await screen.findByRole('button', { name: /add to cart/i }));

    await waitFor(() => expect(callsTo('POST', '/api/cart/items')).toHaveLength(1));
    expect(callsTo('POST', '/api/cart/items')[0].body).toEqual({ productId: jersey.id, quantity: 1 });
    expect(await screen.findByRole('button', { name: /added/i })).toBeInTheDocument();
  });

  it('asks anonymous visitors to log in instead of calling the cart API', async () => {
    const { callsTo } = mockApi({ 'GET /api/products': { body: page([jersey]) } });
    renderApp(<CatalogPage />);

    await userEvent.click(await screen.findByRole('button', { name: /add to cart/i }));

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument();
    expect(callsTo('POST', '/api/cart/items')).toHaveLength(0);
  });

  it('disables the button for products that are out of stock', async () => {
    mockApi({ 'GET /api/products': { body: page([{ ...jersey, stockQuantity: 0 }]) } });
    renderApp(<CatalogPage />);

    expect(await screen.findByRole('button', { name: /add to cart/i })).toBeDisabled();
    expect(screen.getByText('Out of stock')).toBeInTheDocument();
  });
});
