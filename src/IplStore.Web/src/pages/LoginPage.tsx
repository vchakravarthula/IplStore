import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ErrorMessage } from '../components/ErrorMessage';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const from = (location.state as { from?: string } | null)?.from ?? '/';

  async function signIn(username: string) {
    setError(null);
    setBusy(true);
    try {
      await login(username);
      navigate(from, { replace: true });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel narrow">
      <h2>Log in</h2>
      <p className="muted">
        Demo environment: pick a seeded user. The API issues a development token; in production this page redirects to
        Microsoft Entra External ID.
      </p>
      <div className="actions">
        <button className="primary" disabled={busy} onClick={() => signIn('customer')}>
          Continue as Customer
        </button>
        <button disabled={busy} onClick={() => signIn('admin')}>
          Continue as Admin
        </button>
      </div>
      <ErrorMessage error={error} />
    </section>
  );
}
