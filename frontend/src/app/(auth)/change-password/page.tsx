import { ChangePasswordForm } from "../password-forms";
import { AuthHeading } from "../auth-ui";

export const metadata = { title: "Choose your password" };

export default function ForcedChangePasswordPage() {
  return (
    <>
      <AuthHeading title="Choose your password" description="One more step before you start." />
      <ChangePasswordForm forced />
    </>
  );
}
