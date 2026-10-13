import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LoginPage } from '../pages/LoginPage';
import { mockApi, problem, renderApp, signedInSession } from '../test/utils';

describe('login', () => {
  it('signs in as the chosen demo user and goes back to where the user came from', async () => {
    const { callsTo } = mockApi({
      'POST /api/auth/token': { body: { accessToken: 'tok-1', tokenType: 'Bearer', expiresInMinutes: 60, role: 'Customer' } },
      'GET /api/cart': { body: { items: [], total: 0 } }
    });
    renderApp(<LoginPage />, { route: { pathname: '/login', state: { from: '/cart' } }, path: '/login' });

    await userEvent.click(screen.getByRole('button', { name: /continue as customer/i }));

    expect(await screen.findByText('CART PAGE')).toBeInTheDocument();   // returned to where the user came from

    await waitFor(() => expect(callsTo('POST', '/api/auth/token')).toHaveLength(1));
    expect(callsTo('POST', '/api/auth/token')[0].body).toEqual({ username: 'customer' });
    expect(callsTo('POST', '/api/auth/token')[0].headers.Authorization).toBeUndefined();
    await waitFor(() => expect(JSON.parse(sessionStorage.getItem('iplstore.session')!)).toMatchObject({ token: 'tok-1', role: 'Customer' }));
  });

  it('shows the API error when sign-in fails and stores nothing', async () => {
    mockApi({ 'POST /api/auth/token': problem(404, 'Resource not found', 'Unknown demo user.') });
    renderApp(<LoginPage />, { route: '/login', path: '/login' });

    await userEvent.click(screen.getByRole('button', { name: /continue as admin/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Unknown demo user.');
    expect(sessionStorage.getItem('iplstore.session')).toBeNull();
  });
});

describe('route guard', () => {
  it('sends anonymous visitors to the login page', async () => {
    mockApi({});
    renderApp(<div>SECRET CART</div>, { route: '/secret', path: '/secret', guarded: true });

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument();
    expect(screen.queryByText('SECRET CART')).not.toBeInTheDocument();
  });

  it('lets signed-in users through', async () => {
    signedInSession();
    mockApi({ 'GET /api/cart': { body: { items: [], total: 0 } } });
    renderApp(<div>SECRET CART</div>, { route: '/secret', path: '/secret', guarded: true });

    expect(await screen.findByText('SECRET CART')).toBeInTheDocument();
  });
});
