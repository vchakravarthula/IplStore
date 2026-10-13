import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ErrorMessage } from '../components/ErrorMessage';
import { formatInr } from '../lib/format';
import { useAddToCart, useProduct } from '../lib/queries';

const MAX_QUANTITY = 10;   // mirrors CartRules.MaxQuantityPerItem on the server (the server is the authority)

export function ProductPage() {
  const { id = '' } = useParams();
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const { data: product, isLoading, error } = useProduct(id);
  const addToCart = useAddToCart();
  const [quantity, setQuantity] = useState(1);

  if (isLoading) return <p>Loading...</p>;
  if (error) return <ErrorMessage error={error} />;
  if (!product) return null;

  const max = Math.min(MAX_QUANTITY, product.stockQuantity);

  function add() {
    if (!isAuthenticated) {
      navigate('/login', { state: { from: `/products/${id}` } });
      return;
    }
    addToCart.mutate({ productId: id, quantity }, { onSuccess: () => navigate('/cart') });
  }

  return (
    <article className="panel">
      <p>
        <Link to="/">← Back to catalog</Link>
      </p>
      <h2>{product.name}</h2>
      <p className="muted">
        {product.franchiseName} ({product.franchiseCode}) · {product.categoryName} · SKU {product.sku}
      </p>
      <p>{product.description}</p>
      <p className="price">{formatInr(product.price)}</p>
      <p className={product.stockQuantity <= 5 ? 'warn' : 'muted'}>
        {product.stockQuantity === 0 ? 'Out of stock' : `${product.stockQuantity} in stock`}
      </p>
      {product.stockQuantity > 0 && (
        <div className="actions">
          <label>
            Quantity{' '}
            <select value={quantity} onChange={(e) => setQuantity(Number(e.target.value))}>
              {Array.from({ length: max }, (_, i) => i + 1).map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
          <button className="primary" onClick={add} disabled={addToCart.isPending}>
            Add to cart
          </button>
        </div>
      )}
      <ErrorMessage error={addToCart.error} />
    </article>
  );
}
