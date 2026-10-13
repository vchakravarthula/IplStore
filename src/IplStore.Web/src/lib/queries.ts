import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth/AuthContext';
import { api } from './api';
import type { Cart, CheckoutResponse, OrderDetail, OrderSummary, Paged, Product } from './types';

// ---------- catalog (public: never sends the token, so responses stay cacheable) ----------

export interface ProductFilters {
  search?: string;
  sortBy?: 'name' | 'price';
  desc?: boolean;
  page?: number;
  pageSize?: number;
}

export function useProducts(filters: ProductFilters) {
  const params = new URLSearchParams();
  if (filters.search) params.set('search', filters.search);
  if (filters.sortBy) params.set('sortBy', filters.sortBy);
  if (filters.desc) params.set('desc', 'true');
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 12));

  return useQuery({
    queryKey: ['products', params.toString()],
    queryFn: () => api<Paged<Product>>(`/api/products?${params}`, { auth: false }),
    placeholderData: keepPreviousData
  });
}

export function useProduct(id: string) {
  return useQuery({
    queryKey: ['product', id],
    queryFn: () => api<Product>(`/api/products/${id}`, { auth: false })
  });
}

// ---------- cart ----------

export function useCart() {
  const { isAuthenticated } = useAuth();
  return useQuery({
    queryKey: ['cart'],
    queryFn: () => api<Cart>('/api/cart'),
    enabled: isAuthenticated
  });
}

// Every cart endpoint returns the updated cart, so the cache is replaced without another round-trip.
function useCartMutation<TVariables>(fn: (variables: TVariables) => Promise<Cart>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (cart) => queryClient.setQueryData(['cart'], cart)
  });
}

export const useAddToCart = () =>
  useCartMutation(({ productId, quantity }: { productId: string; quantity: number }) =>
    api<Cart>('/api/cart/items', { method: 'POST', body: { productId, quantity } }));   // note: no price is ever sent

export const useSetCartQuantity = () =>
  useCartMutation(({ productId, quantity }: { productId: string; quantity: number }) =>
    api<Cart>(`/api/cart/items/${productId}`, { method: 'PUT', body: { quantity } }));

export const useRemoveFromCart = () =>
  useCartMutation((productId: string) => api<Cart>(`/api/cart/items/${productId}`, { method: 'DELETE' }));

// ---------- orders ----------

export function useOrders(page: number) {
  const { isAuthenticated } = useAuth();
  return useQuery({
    queryKey: ['orders', page],
    queryFn: () => api<Paged<OrderSummary>>(`/api/orders?page=${page}&pageSize=10`),
    enabled: isAuthenticated,
    placeholderData: keepPreviousData
  });
}

export function useOrder(id: string) {
  return useQuery({
    queryKey: ['order', id],
    queryFn: () => api<OrderDetail>(`/api/orders/${id}`)
  });
}

export function useCheckout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ idempotencyKey }: { idempotencyKey: string }) =>
      api<CheckoutResponse>('/api/orders/checkout', { method: 'POST', headers: { 'Idempotency-Key': idempotencyKey } }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['cart'] });
      queryClient.invalidateQueries({ queryKey: ['orders'] });
    }
  });
}

// ---------- payment ----------

export const usePaymentConfig = () =>
  useQuery({ queryKey: ['payment-config'], queryFn: () => api<{ keyId: string }>('/api/payments/config'), staleTime: Infinity });

function useAfterPayment() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['orders'] });
    queryClient.invalidateQueries({ queryKey: ['order'] });
    queryClient.invalidateQueries({ queryKey: ['cart'] });   // the server removes the paid items from the cart on success
  };
}

export function useConfirmPayment() {
  const afterPayment = useAfterPayment();
  return useMutation({
    mutationFn: (v: { orderId: string; gatewayOrderId: string; gatewayPaymentId: string; signature: string }) =>
      api<{ orderId: string; status: string }>(`/api/orders/${v.orderId}/confirm-payment`, {
        method: 'POST',
        body: { gatewayOrderId: v.gatewayOrderId, gatewayPaymentId: v.gatewayPaymentId, signature: v.signature }
      }),
    onSuccess: afterPayment
  });
}

/** Development-only shortcut: the API signs the payment itself. Never available outside Development. */
export function useSimulatePayment() {
  const afterPayment = useAfterPayment();
  return useMutation({
    mutationFn: (orderId: string) =>
      api<{ orderId: string; status: string }>(`/api/dev/orders/${orderId}/simulate-payment`, { method: 'POST' }),
    onSuccess: afterPayment
  });
}

// ---------- admin ----------

export const useRestock = () =>
  useMutation({
    mutationFn: (v: { productId: string; quantity: number }) =>
      api<void>(`/api/admin/products/${v.productId}/restock`, { method: 'POST', body: { quantity: v.quantity } })
  });
