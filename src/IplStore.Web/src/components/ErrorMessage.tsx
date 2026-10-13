import { ApiError } from '../lib/api';

/** Shows a failed request. The trace id lets support find the exact request in the telemetry dashboard. */
export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null;

  if (error instanceof ApiError) {
    return (
      <div role="alert" className="error">
        <strong>{error.title}</strong>: {error.message}
        {error.traceId && <small> (trace {error.traceId})</small>}
      </div>
    );
  }

  const message = error instanceof Error ? error.message : 'Something went wrong.';
  return (
    <div role="alert" className="error">
      {message}
    </div>
  );
}
