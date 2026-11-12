import { useEffect, useRef } from 'react';
import type { DiagramStatus } from './types';

interface UseRenderStatusOptions {
  status?: DiagramStatus;
  onRefresh: () => Promise<void> | void;
  intervalMs?: number;
}

/**
 * Hook polling automatic refresh for diagrams currently in Pending or Rendering state.
 * Polls every 2 seconds by default until status becomes Rendered or Failed.
 */
export function useRenderStatus({
  status,
  onRefresh,
  intervalMs = 2000,
}: UseRenderStatusOptions) {
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    const isBusy = status === 'Pending' || status === 'Rendering';

    if (isBusy) {
      timerRef.current = setInterval(async () => {
        await onRefresh();
      }, intervalMs);
    }

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [status, onRefresh, intervalMs]);
}
