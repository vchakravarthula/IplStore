// Mirrors the API's DTOs (camelCase JSON).

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Product {
  id: string;
  name: string;
  sku: string;
  description: string;
  price: number;
  stockQuantity: number;
  imageUrl: string;
  franchiseId: string;
  franchiseName: string;
  franchiseCode: string;
  categoryId: string;
  categoryName: string;
}

export interface CartItem {
  productId: string;
  name: string;
  imageUrl: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  availableStock: number;
}

export interface Cart {
  items: CartItem[];
  total: number;
}

export interface CheckoutResponse {
  orderId: string;
  shortOrderNumber: string;
  totalAmount: number;
  currency: string;
  status: string;
  gatewayOrderId: string | null;
}

export interface OrderSummary {
  id: string;
  shortOrderNumber: string;
  status: string;
  totalAmount: number;
  itemCount: number;
  createdAtUtc: string;
}

export interface OrderItem {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface OrderDetail {
  id: string;
  shortOrderNumber: string;
  status: string;
  totalAmount: number;
  gatewayOrderId: string | null;
  createdAtUtc: string;
  items: OrderItem[];
}

export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresInMinutes: number;
  role: string;
}
