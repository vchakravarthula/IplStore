const LABELS: Record<string, string> = {
  PendingPayment: 'Pending payment',
  Paid: 'Paid',
  Failed: 'Failed',
  Cancelled: 'Cancelled (expired)',
  Processing: 'Processing',
  Completed: 'Completed'
};

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{LABELS[status] ?? status}</span>;
}
