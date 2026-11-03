import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/** True after hydration. Use for values only known in the browser (theme, locale, storage). */
export function useMounted() {
  return useSyncExternalStore(subscribe, () => true, () => false);
}
