import { Suspense } from "react";
import { PublicApply, PublicApplySkeleton } from "./apply-form";

/**
 * Public online application. The school comes from the URL, which is only known per request, so the form streams in
 * under Suspense while the page shell prerenders.
 */
export default function PublicApplyPage() {
  return (
    <Suspense fallback={<PublicApplySkeleton />}>
      <PublicApply />
    </Suspense>
  );
}
