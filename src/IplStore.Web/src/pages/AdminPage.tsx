import { useState } from 'react';
import { ErrorMessage } from '../components/ErrorMessage';
import { useProducts, useRestock } from '../lib/queries';

/** Demonstrates policy-based authorization end to end: the API rejects non-admin tokens with 403 regardless of this UI. */
export function AdminPage() {
  const { data } = useProducts({ pageSize: 50 });
  const restock = useRestock();
  const [productId, setProductId] = useState('');
  const [quantity, setQuantity] = useState(10);
  const [done, setDone] = useState<string | null>(null);

  function submit(e: React.SyntheticEvent) {
    e.preventDefault();
    setDone(null);
    restock.mutate({ productId, quantity }, { onSuccess: () => setDone(`Added ${quantity} units.`) });
  }

  return (
    <section className="panel">
      <h2>Admin: restock</h2>
      <form onSubmit={submit} className="actions">
        <select aria-label="Product" value={productId} onChange={(e) => setProductId(e.target.value)} required>
          <option value="">Select a product</option>
          {data?.items.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} (stock {p.stockQuantity})
            </option>
          ))}
        </select>
        <input type="number" min={1} aria-label="Quantity" value={quantity} onChange={(e) => setQuantity(Number(e.target.value))} />
        <button className="primary" type="submit" disabled={restock.isPending || !productId}>
          Restock
        </button>
      </form>
      {done && <p className="success-text">{done}</p>}
      <ErrorMessage error={restock.error} />
      <p className="muted">Catalog pages may show stock up to 30 seconds late because the catalog is cached.</p>
    </section>
  );
}
