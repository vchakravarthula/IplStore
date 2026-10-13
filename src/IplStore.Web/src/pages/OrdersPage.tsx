import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorMessage } from '../components/ErrorMessage';
import { StatusBadge } from '../components/StatusBadge';
import { formatDate, formatInr } from '../lib/format';
import { useOrders } from '../lib/queries';

export function OrdersPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, error } = useOrders(page);

  if (isLoading) return <p>Loading orders...</p>;
  if (error) return <ErrorMessage error={error} />;

  if (!data || data.items.length === 0) {
    return (
      <section className="panel">
        <h2>Order history</h2>
        <p>You have not placed any orders yet.</p>
        <Link to="/">Start shopping</Link>
      </section>
    );
  }

  return (
    <section>
      <h2>Order history</h2>
      <table>
        <thead>
          <tr>
            <th>Order</th>
            <th>Placed</th>
            <th>Items</th>
            <th>Total</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((order) => (
            <tr key={order.id}>
              <td>
                <Link to={`/orders/${order.id}`}>{order.shortOrderNumber}</Link>
              </td>
              <td>{formatDate(order.createdAtUtc)}</td>
              <td>{order.itemCount}</td>
              <td>{formatInr(order.totalAmount)}</td>
              <td>
                <StatusBadge status={order.status} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {data.totalPages > 1 && (
        <div className="pager">
          <button disabled={page <= 1} onClick={() => setPage(page - 1)}>
            Previous
          </button>
          <span>
            Page {data.page} of {data.totalPages}
          </span>
          <button disabled={page >= data.totalPages} onClick={() => setPage(page + 1)}>
            Next
          </button>
        </div>
      )}
    </section>
  );
}
