import { notFound } from "next/navigation";
import { DesignSystemPreview } from "./preview";

export const metadata = { title: "Design system" };

/** Development-only reference of the design system, used to review every primitive in both themes. */
export default function DesignPage() {
  if (process.env.NODE_ENV === "production") notFound();
  return <DesignSystemPreview />;
}
