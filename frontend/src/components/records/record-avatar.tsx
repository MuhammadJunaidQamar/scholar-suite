"use client";

import * as React from "react";
import { refreshSession } from "@/lib/api/client";
import { fileUrl } from "@/lib/api/students";
import { Avatar } from "@/components/ui/display";

// Photos are private files served from /api with the session cookie. An <img> can't refresh an expired session
// itself, so the first failure refreshes once (shared by every photo on screen) and bumps a generation number that
// makes each photo request again.
let generation = 0;
const listeners = new Set<() => void>();
let recovering: Promise<void> | null = null;

function recover() {
  recovering ??= refreshSession()
    .then((ok) => {
      if (!ok) return;
      generation++;
      listeners.forEach((listener) => listener());
    })
    .finally(() => setTimeout(() => (recovering = null), 10_000));
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function useFileGeneration() {
  return React.useSyncExternalStore(subscribe, () => generation, () => 0);
}

/** A person's photo when there is one, their initials otherwise. */
export function RecordAvatar({ name, fileId, size = 28, square, className }: {
  name: string;
  fileId: string | null | undefined;
  size?: number;
  square?: boolean;
  className?: string;
}) {
  const current = useFileGeneration();
  const retried = React.useRef(false);
  return (
    <Avatar name={name} size={size} square={square} className={className} src={fileId ? fileUrl(fileId, current) : null}
      onImageError={() => {
        // One recovery attempt per photo: a genuinely missing file must not loop.
        if (retried.current) return;
        retried.current = true;
        recover();
      }} />
  );
}
