const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' });

export const formatInr = (amount: number) => inr.format(amount);

export const formatDate = (iso: string) =>
  new Date(iso.endsWith('Z') ? iso : `${iso}Z`).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' });
