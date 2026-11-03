import { Suspense } from "react";
import { SetPasswordForm } from "../password-forms";

export const metadata = { title: "Welcome" };

export default function AcceptInvitePage() {
  return (
    <Suspense>
      <SetPasswordForm mode="invite" />
    </Suspense>
  );
}
