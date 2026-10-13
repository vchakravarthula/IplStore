# Demo checklist

## 30 minutes before
- [ ] `docker compose -f docker-compose.infra.yml up -d` and all four containers healthy (`docker compose ps`)
- [ ] Reset to a known state: drop the `IplStore` database (or `docker compose down -v` then `up -d`), `redis-cli FLUSHDB`
- [ ] API running: `cd src/IplStore.Api; dotnet run --launch-profile http`, then open `/health/ready` (must say Healthy)
- [ ] UI running: `cd src/IplStore.Web; npm install; npm run dev` and open http://localhost:5173
- [ ] Browser tabs ready: UI, RabbitMQ (15672), Aspire dashboard (18888), Razorpay test dashboard
- [ ] `dotnet test --filter-not-trait "Category=Redis"` and `npm test` are green
- [ ] Network check: Razorpay popup opens. If not, the "Simulate payment (dev only)" button is the fallback.

## Core flow (the five requirements)
1. **Product list + search**: search `csk`, `jersey`, sort by price, page through. (Requirement 1 and 3)
2. **Product details**: open a product. (Requirement 2)
3. **Cart**: log in as Customer, add 2 items, change a quantity, remove one. Show Redis: `redis-cli HGETALL cart:customer-1`. (Requirement 4)
4. **Checkout + payment**: place the order, pay in the Razorpay popup. Use the DOMESTIC test card `4386 2894 0766 0153` (any CVV, any future expiry, untick "Save card"), then enter any 4-10 digit OTP such as `123456`. (`4111 1111 1111 1111` is treated as international and is rejected on this account.) UPI `success@razorpay` also works.
   Show: stock decreased in SQL, outbox rows processed, queue message in RabbitMQ, trace with the Razorpay call in the dashboard.
5. **Order history**: open Orders, then an order's detail (price snapshot per line). (Requirement 5)

## Production scenarios to show (each is a test too)
| Scenario | How | What to point at |
|---|---|---|
| Oversell protection | Photos have 5 units: cart 3 as customer, then admin tries 3 | 409, stock never negative |
| Double click / retry | Checkout twice with the same key (UI reuses it after a network error) | one order, stock deducted once |
| Failed / abandoned payment | Close the Razorpay popup or use an OTP shorter than 4 digits, then go back to the cart | cart still full; pressing Place order again returns the SAME open order (no second reservation) |
| Payment provider down | Wrong `Razorpay:KeySecret`, checkout | 502, order Failed, stock restored, cart kept |
| Abandoned order | `Orders:PendingPaymentTimeoutMinutes=1`, place order, wait | Cancelled, stock restored |
| Redis down | `docker stop <redis>` then open the cart | 503 + Retry-After, readiness unhealthy, auto-recovers |
| Broker down | `docker stop <rabbitmq>`, checkout, watch `Attempts` | no lost event; published after restart |
| Forged payment | confirm-payment with a bad signature | 400, order stays pending |
| Stolen order id | Admin opens the customer's order URL | 404 |
| Authorization | Customer calls `/api/admin/...` | 403; Admin restock works (Admin page in the UI) |

## Enhancement talking points (admin management is implemented as restock only)
Admin product CRUD, Razorpay webhook (`payment.captured`) as the reliable second confirmation path, receipt e-mail and invoice PDF
consumers on the outbox queues, HybridCache + Redis for the catalog, rate limiting, Entra External ID login, Azure Container Apps + autoscale.

## Questions to be ready for
Why atomic UPDATE instead of rowversion retries? Why is the cart not a domain aggregate? What happens if the process dies between
publish and mark-processed (at-least-once, idempotent consumers on EventId)? Why is the idempotency key rotated after a 409 but kept after a 503?
