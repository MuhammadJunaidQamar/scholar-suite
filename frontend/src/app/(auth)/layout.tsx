import { productName } from "@/lib/brand";

/**
 * Sign-in surfaces borrow from the school exercise book: ruled lines, a red margin rule, and the product's promise
 * written on the lines. Restrained enough to disappear once you start typing.
 */
export default function AuthLayout({ children }: LayoutProps<"/">) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,1.05fr)_minmax(0,1fr)]">
      <aside
        aria-hidden
        className="relative hidden overflow-hidden border-r border-line bg-sunken lg:block"
        style={{
          backgroundImage: "repeating-linear-gradient(to bottom, transparent 0, transparent 39px, var(--line) 39px, var(--line) 40px)",
          backgroundPosition: "0 23px",
        }}
      >
        <div className="absolute inset-y-0 left-24 w-px bg-[oklch(0.7_0.12_22/0.55)] dark:bg-[oklch(0.55_0.12_22/0.6)]" />
        <div className="absolute inset-y-0 left-[100px] w-px bg-[oklch(0.7_0.12_22/0.3)] dark:bg-[oklch(0.55_0.12_22/0.35)]" />
        <div className="relative flex h-full flex-col justify-between py-[63px] pr-16 pl-32">
          <p className="font-display text-xl leading-[40px] text-ink">{productName}</p>
          <div className="max-w-lg">
            <p className="font-display text-[30px] leading-[40px] text-ink">
              Every register, ledger and report card, kept in order.
            </p>
            <p className="mt-[40px] max-w-md text-base leading-[40px] text-ink-2">
              Admissions to alumni, attendance to accounts.
            </p>
          </div>
          <p className="text-sm leading-[40px] text-ink-3">{productName} · School management</p>
        </div>
      </aside>
      <main className="flex items-center justify-center px-6 py-12">
        <div className="w-full max-w-[360px]">{children}</div>
      </main>
    </div>
  );
}
