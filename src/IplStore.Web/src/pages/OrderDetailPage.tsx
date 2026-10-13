import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ErrorMessage } from '../components/ErrorMessage';
import { PaymentPanel } from '../components/PaymentPanel';
import { StatusBadge } from '../components/StatusBadge';
import { formatDate, formatInr } from '../lib/format';
import { useOrder } from '../lib/queries';

export function OrderDetailPage() {
  const { id = '' } = useParams();
  const { data: order, isLoading, error } = useOrder(id);
  const [justPaid, setJustPaid] = useState(false);

  if (isLoading) return <p>Loading order...</p>;
  if (error) return <ErrorMessage error={error} />;
  if (!order) return null;

  const canPay = order.status === 'PendingPayment' && order.gatewayOrderId && !justPaid;

  return (
    <section>
      <p>
        <Link to="/orders">← All orders</Link>
      </p>
      <h2>Order {order.shortOrderNumber}</h2>
      <p>
        <StatusBadge status={justPaid ? 'Paid' : order.status} /> · placed {formatDate(order.createdAtUtc)}
      </p>
      <table>
        <thead>
          <tr>
            <th>Product</th>
            <th>Quantity</th>
            <th>Unit price</th>
            <th>Total</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.productId}>
              <td>{item.productName}</td>
              <td>{item.quantity}</td>
              <td>{formatInr(item.unitPrice)}</td>
              <td>{formatInr(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={3}>
              <strong>Total</strong>
            </td>
            <td>
              <strong>{formatInr(order.totalAmount)}</strong>
            </td>
          </tr>
        </tfoot>
      </table>
      {canPay && order.gatewayOrderId && (
        <PaymentPanel
          orderId={order.id}
          gatewayOrderId={order.gatewayOrderId}
          amount={order.totalAmount}
          currency="INR"
          onPaid={() => setJustPaid(true)}
        />
      )}
    </section>
  );
}
