import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ErrorMessage } from '../components/ErrorMessage';
import { formatInr } from '../lib/format';
import { useAddToCart, useProducts } from '../lib/queries';
import type { Product } from '../lib/types';

type SortOption = 'name-asc' | 'price-asc' | 'price-desc';

export function CatalogPage() {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const addToCart = useAddToCart();

  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<SortOption>('name-asc');
  const [page, setPage] = useState(1);
  const [addedId, setAddedId] = useState<string | null>(null);

  // Wait for the user to stop typing before querying (keeps the API and its cache from being hammered).
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const { data, isLoading, error } = useProducts({
    search,
    sortBy: sort === 'name-asc' ? 'name' : 'price',
    desc: sort === 'price-desc',
    page
  });

  function add(product: Product) {
    if (!isAuthenticated) {
      navigate('/login', { state: { from: '/' } });
      return;
    }
    addToCart.mutate({ productId: product.id, quantity: 1 }, { onSuccess: () => setAddedId(product.id) });
  }

  return (
    <>
      <h2>Catalog</h2>
      <div className="toolbar">
        <input
          type="search"
          placeholder="Search name, franchise (CSK), type (jersey)..."
          aria-label="Search products"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
        <select aria-label="Sort products" value={sort} onChange={(e) => { setSort(e.target.value as SortOption); setPage(1); }}>
          <option value="name-asc">Name</option>
          <option value="price-asc">Price: low to high</option>
          <option value="price-desc">Price: high to low</option>
        </select>
      </div>

      <ErrorMessage error={error} />
      <ErrorMessage error={addToCart.error} />
      {isLoading && <p>Loading products...</p>}
      {data && data.items.length === 0 && <p>No products match "{search}".</p>}

      <div className="grid">
        {data?.items.map((product) => (
          <article key={product.id} className="card">
            <h3>
              <Link to={`/products/${product.id}`}>{product.name}</Link>
            </h3>
            <p className="muted">
              {product.franchiseName} · {product.categoryName}
            </p>
            <p className="price">{formatInr(product.price)}</p>
            <p className={product.stockQuantity <= 5 ? 'warn' : 'muted'}>
              {product.stockQuantity === 0 ? 'Out of stock' : product.stockQuantity <= 5 ? `Only ${product.stockQuantity} left` : 'In stock'}
            </p>
            <button className="primary" disabled={product.stockQuantity === 0 || addToCart.isPending} onClick={() => add(product)}>
              {addedId === product.id ? 'Added ✓' : 'Add to cart'}
            </button>
          </article>
        ))}
      </div>

      {data && data.totalPages > 1 && (
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
    </>
  );
}
