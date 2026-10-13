import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useCart } from '../lib/queries';

export function Layout() {
  const { session, isAuthenticated, isAdmin, logout } = useAuth();
  const { data: cart } = useCart();
  const navigate = useNavigate();
  const cartCount = cart?.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0;

  return (
    <>
      <header className="header">
        <Link to="/" className="brand">
          IPL Store
        </Link>
        <nav>
          <NavLink to="/">Catalog</NavLink>
          <NavLink to="/cart">Cart{cartCount > 0 && <span className="pill" aria-label="items in cart">{cartCount}</span>}</NavLink>
          <NavLink to="/orders">Orders</NavLink>
          {isAdmin && <NavLink to="/admin">Admin</NavLink>}
        </nav>
        <div className="user">
          {isAuthenticated ? (
            <>
              <span>
                {session?.username} ({session?.role})
              </span>
              <button
                onClick={() => {
                  logout();
                  navigate('/');
                }}
              >
                Log out
              </button>
            </>
          ) : (
            <Link to="/login">Log in</Link>
          )}
        </div>
      </header>
      <main className="container">
        <Outlet />
      </main>
    </>
  );
}
