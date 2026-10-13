import { useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { formatInr } from '../lib/format';
import { useConfirmPayment, usePaymentConfig, useSimulatePayment } from '../lib/queries';
import { loadRazorpay } from '../lib/razorpay';
import { ErrorMessage } from './ErrorMessage';

interface Props {
  orderId: string;
  gatewayOrderId: string;
  amount: number;
  currency: string;
  onPaid: (status: string) => void;
}

// Visible only when the web app is started with VITE_ENABLE_SIMULATED_PAYMENT=true; the API refuses it outside Development.
const simulatedPaymentEnabled = () => import.meta.env.VITE_ENABLE_SIMULATED_PAYMENT === 'true';

// DEMO ONLY: the seeded users have no phone number or profile. A real app prefills these from the customer's account.
const DEMO_CONTACT = '9999999999';

export function PaymentPanel({ orderId, gatewayOrderId, amount, currency, onPaid }: Props) {
  const { session } = useAuth();
  const config = usePaymentConfig();
  const confirm = useConfirmPayment();
  const simulate = useSimulatePayment();
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const busy = confirm.isPending || simulate.isPending;

  async function payWithRazorpay() {
    setError(null);
    setNotice(null);
    try {
      const keyId = config.data?.keyId;
      if (!keyId) throw new Error('Payment configuration is not available yet. Please retry.');

      const Razorpay = await loadRazorpay();
      const checkout = new Razorpay({
        key: keyId,
        order_id: gatewayOrderId,
        amount: Math.round(amount * 100),
        currency,
        name: 'IPL Store',
        description: 'Order payment',
        prefill: {
          name: session?.username ?? '',
          email: `${session?.username ?? 'customer'}@iplstore.local`,
          contact: DEMO_CONTACT
        },
        // Razorpay calls this with a SIGNED proof of payment. The API verifies the signature before marking the order paid.
        handler: async (response) => {
          try {
            const result = await confirm.mutateAsync({
              orderId,
              gatewayOrderId: response.razorpay_order_id,
              gatewayPaymentId: response.razorpay_payment_id,
              signature: response.razorpay_signature
            });
            onPaid(result.status);
          } catch (e) {
            setError(e);
          }
        },
        modal: {
          ondismiss: () => setNotice('Payment window closed. Your order is held for a short time; you can resume from Orders.')
        }
      });
      checkout.on('payment.failed', (r) => setNotice(`Payment failed: ${r.error.description}. You can try again.`));
      checkout.open();
    } catch (e) {
      setError(e);
    }
  }

  async function simulatePayment() {
    setError(null);
    setNotice(null);
    try {
      const result = await simulate.mutateAsync(orderId);
      onPaid(result.status);
    } catch (e) {
      setError(e);
    }
  }

  return (
    <section className="panel" aria-label="Payment">
      <h3>Pay {formatInr(amount)}</h3>
      <div className="actions">
        <button className="primary" onClick={payWithRazorpay} disabled={busy}>
          Pay with Razorpay
        </button>
        {simulatedPaymentEnabled() && (
          <button onClick={simulatePayment} disabled={busy} title="Development only: the API signs the payment itself">
            Simulate payment (dev only)
          </button>
        )}
      </div>
      {notice && <p className="muted">{notice}</p>}
      <ErrorMessage error={error} />
    </section>
  );
}
