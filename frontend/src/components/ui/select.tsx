"use client";

import * as React from "react";
import { Select as SelectPrimitive } from "radix-ui";
import { Check, ChevronsUpDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { controlBase } from "./input";

export type SelectOption = { value: string; label: React.ReactNode; description?: React.ReactNode; disabled?: boolean };

type SelectProps = {
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
  options: SelectOption[];
  placeholder?: string;
  disabled?: boolean;
  id?: string;
  name?: string;
  className?: string;
  size?: "sm" | "md";
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  "aria-label"?: string;
};

/** Accessible custom select (Radix) for short option lists. Use a Combobox for long, searchable lists. */
export function Select({ options, placeholder = "Select…", className, size = "md", id, ...props }: SelectProps) {
  return (
    <SelectPrimitive.Root value={props.value} defaultValue={props.defaultValue} onValueChange={props.onValueChange}
      disabled={props.disabled} name={props.name}>
      <SelectPrimitive.Trigger
        id={id}
        aria-invalid={props["aria-invalid"]}
        aria-describedby={props["aria-describedby"]}
        aria-label={props["aria-label"]}
        className={cn(
          controlBase,
          "flex items-center justify-between gap-2 px-2.5 text-left data-[placeholder]:text-ink-3",
          size === "sm" ? "h-7 text-sm" : "h-8 text-base",
          className,
        )}
      >
        <span className="truncate">
          <SelectPrimitive.Value placeholder={placeholder} />
        </span>
        <SelectPrimitive.Icon asChild>
          <ChevronsUpDown className="size-3.5 shrink-0 text-ink-3" />
        </SelectPrimitive.Icon>
      </SelectPrimitive.Trigger>
      <SelectPrimitive.Portal>
        <SelectPrimitive.Content
          position="popper"
          sideOffset={4}
          className={cn(
            "z-50 max-h-[min(22rem,var(--radix-select-content-available-height))] min-w-[var(--radix-select-trigger-width)]",
            "overflow-hidden rounded-lg border border-line bg-raised shadow-popover data-[state=open]:animate-pop-in",
          )}
        >
          <SelectPrimitive.Viewport className="p-1 scrollbar-thin">
            {options.map((option) => (
              <SelectPrimitive.Item
                key={option.value}
                value={option.value}
                disabled={option.disabled}
                className={cn(
                  "relative flex cursor-default items-start gap-2 rounded-md py-1.5 pr-8 pl-2 text-base outline-none select-none",
                  "data-[highlighted]:bg-sunken data-[disabled]:opacity-50",
                )}
              >
                <div className="grid">
                  <SelectPrimitive.ItemText>{option.label}</SelectPrimitive.ItemText>
                  {option.description && <span className="text-xs text-ink-2">{option.description}</span>}
                </div>
                <SelectPrimitive.ItemIndicator className="absolute top-2 right-2 text-accent">
                  <Check className="size-4" />
                </SelectPrimitive.ItemIndicator>
              </SelectPrimitive.Item>
            ))}
          </SelectPrimitive.Viewport>
        </SelectPrimitive.Content>
      </SelectPrimitive.Portal>
    </SelectPrimitive.Root>
  );
}
