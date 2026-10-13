import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';
import { setAccessToken, setUnauthorizedHandler } from '../lib/api';

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  setAccessToken(null);
  setUnauthorizedHandler(null);
  delete window.Razorpay;
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
});
