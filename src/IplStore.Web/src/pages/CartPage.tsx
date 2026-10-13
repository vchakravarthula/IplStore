import { Link } from 'react-router-dom';
import { ErrorMessage } from '../components/ErrorMessage';
import { formatInr } from '../lib/format';
import { useCart, useRemoveFromCart, useSetCartQuantity } from '../lib/queries';

const MAX_QUANTITY = 10;

export function CartPage() {
  const { data: cart, isLoading, error } = useCart();
  const setQuantity = useSetCartQuantity();
  const remove = useRemoveFromCart();

  if (isLoading) return <p>Loading cart...</p>;
  if (error) return <ErrorMessage error={error} />;

  if (!cart || cart.items.length === 0) {
    return (
      <section className="panel">
        <h2>Your cart</h2>
        <p>Your cart is empty.</p>
        <Link to="/">Browse the catalog</Link>
      </section>
    );
  }

  return (
    <section>
      <h2>Your cart</h2>
      <table>
        <thead>
          <tr>
            <th>Product</th>
            <th>Price</th>
            <th>Quantity</th>
            <th>Total</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {cart.items.map((item) => (
            <tr key={item.productId}>
              <td>{item.name}</td>
              <td>{formatInr(item.unitPrice)}</td>
              <td>
                <select
                  aria-label={`Quantity for ${item.name}`}
                  value={item.quantity}
                  disabled={setQuantity.isPending}
                  onChange={(e) => setQuantity.mutate({ productId: item.productId, quantity: Number(e.target.value) })}
                >
                  {Array.from({ length: Math.min(MAX_QUANTITY, Math.max(item.availableStock, item.quantity)) }, (_, i) => i + 1).map((n) => (
                    <option key={n} value={n}>
                      {n}
                    </option>
                  ))}
                </select>
              </td>
              <td>{formatInr(item.lineTotal)}</td>
              <td>
                <button onClick={() => remove.mutate(item.productId)} disabled={remove.isPending}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={3}>
              <strong>Total</strong>
            </td>
            <td colSpan={2}>
              <strong>{formatInr(cart.total)}</strong>
            </td>
          </tr>
        </tfoot>
      </table>
      <ErrorMessage error={setQuantity.error} />
      <ErrorMessage error={remove.error} />
      <p className="actions">
        <Link to="/checkout" className="button primary">
          Proceed to checkout
        </Link>
      </p>
    </section>
  );
}
