import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorMessage } from '../components/ErrorMessage';
import { PaymentPanel } from '../components/PaymentPanel';
import { shouldRotateIdempotencyKey } from '../lib/api';
import { formatInr } from '../lib/format';
import { useCart, useCheckout } from '../lib/queries';
import type { CheckoutResponse } from '../lib/types';

export function CheckoutPage() {
  const { data: cart, isLoading } = useCart();
  const checkout = useCheckout();
  const [order, setOrder] = useState<CheckoutResponse | null>(null);
  const [paidStatus, setPaidStatus] = useState<string | null>(null);

  // ONE key per checkout attempt. Retrying after a network blip reuses it, so the server returns the same order
  // instead of creating a second one. It is only replaced after a definitive failure (see shouldRotateIdempotencyKey).
  const idempotencyKey = useRef(crypto.randomUUID());

  function placeOrder() {
    checkout.mutate(
      { idempotencyKey: idempotencyKey.current },
      {
        onSuccess: (response) => {
          // Anything other than an open order (Failed, Cancelled...) means this key is spent: start a fresh attempt next time.
          if (response.status !== 'PendingPayment') idempotencyKey.current = crypto.randomUUID();
          if (response.status === 'Paid') setPaidStatus('Paid');
          setOrder(response);
        },
        onError: (error) => {
          if (shouldRotateIdempotencyKey(error)) idempotencyKey.current = crypto.randomUUID();
        }
      }
    );
  }

  if (paidStatus && order) {
    return (
      <section className="panel success" aria-label="Order confirmation">
        <h2>Payment successful</h2>
        <p>
          Order <strong>{order.shortOrderNumber}</strong> is <strong>{paidStatus}</strong>. Total {formatInr(order.totalAmount)}.
        </p>
        <Link to={`/orders/${order.orderId}`}>View order</Link> · <Link to="/orders">All orders</Link>
      </section>
    );
  }

  if (order && order.status === 'PendingPayment' && order.gatewayOrderId) {
    return (
      <section>
        <div className="panel">
          <h2>Order {order.shortOrderNumber} placed</h2>
          <p>
            Stock is reserved for you. Complete the payment of <strong>{formatInr(order.totalAmount)}</strong> to confirm it.
          </p>
        </div>
        <PaymentPanel
          orderId={order.orderId}
          gatewayOrderId={order.gatewayOrderId}
          amount={order.totalAmount}
          currency={order.currency}
          onPaid={setPaidStatus}
        />
      </section>
    );
  }

  if (isLoading) return <p>Loading...</p>;

  if (!cart || cart.items.length === 0) {
    return (
      <section className="panel">
        <h2>Checkout</h2>
        <p>Your cart is empty.</p>
        <Link to="/">Browse the catalog</Link>
      </section>
    );
  }

  return (
    <section>
      <h2>Review your order</h2>
      <table>
        <tbody>
          {cart.items.map((item) => (
            <tr key={item.productId}>
              <td>
                {item.name} × {item.quantity}
              </td>
              <td>{formatInr(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td>
              <strong>Total</strong>
            </td>
            <td>
              <strong>{formatInr(cart.total)}</strong>
            </td>
          </tr>
        </tfoot>
      </table>
      {order?.status === 'Failed' && <p className="error">The payment provider could not create the order. Nothing was charged; please try again.</p>}
      <ErrorMessage error={checkout.error} />
      <p className="actions">
        <button className="primary" onClick={placeOrder} disabled={checkout.isPending}>
          {checkout.isPending ? 'Placing order...' : 'Place order'}
        </button>
        <Link to="/cart">Back to cart</Link>
      </p>
    </section>
  );
}
