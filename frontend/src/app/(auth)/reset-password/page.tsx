import { Suspense } from "react";
import { SetPasswordForm } from "../password-forms";

export const metadata = { title: "Choose a new password" };

export default function ResetPasswordPage() {
  return (
    <Suspense>
      <SetPasswordForm mode="reset" />
    </Suspense>
  );
}
