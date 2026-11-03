"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { DoorOpen, Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { pluralize } from "@/lib/utils";
import { useCampuses } from "@/lib/api/access";
import { roomTypeLabel, useDeleteRoom, useRooms, useSaveRoom, type Room, type RoomType } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { RecordList, RecordRow, SectionHeading } from "@/components/ui/record-list";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";

const types = Object.keys(roomTypeLabel) as RoomType[];
const schema = z.object({
  campusId: z.string().min(1, "Choose a campus."),
  code: z.string().trim().min(1, "Enter a code.").max(20),
  name: z.string().trim().min(1, "Enter a name.").max(100),
  type: z.enum(["Classroom", "Laboratory", "ComputerLab", "Library", "Hall", "Sports", "Office", "Staffroom", "Other"]),
  building: z.string().max(80),
  floor: z.string().max(30),
  capacity: z.coerce.number<number>().int().min(0).max(5000),
  isBookable: z.boolean(),
  isActive: z.boolean(),
});
type Values = z.infer<typeof schema>;

function RoomDialog({ room, open, onOpenChange }: { room: Room | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveRoom();
  const remove = useDeleteRoom();
  const campuses = useCampuses();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form}
      defaults={{ campusId: room?.campusId ?? campuses.data?.find((c) => c.isMain)?.id ?? "", code: room?.code ?? "", name: room?.name ?? "",
        type: room?.type ?? "Classroom", building: room?.building ?? "", floor: room?.floor ?? "", capacity: room?.capacity ?? 30,
        isBookable: room?.isBookable ?? false, isActive: room?.isActive ?? true }}
      title={room ? `Edit ${room.name}` : "New room"} submitLabel={room ? "Save room" : "Add room"} successMessage={room ? "Room saved" : "Room added"}
      footerStart={room && (
        <Button type="button" variant="danger-ghost" onClick={async () => {
          try { await remove.mutateAsync(room.id); toast.success("Room removed"); onOpenChange(false); } catch (err) { toast.error(err); }
        }}>Remove</Button>
      )}
      onSubmit={(v) => save.mutateAsync({ id: room?.id, body: { ...v, building: nullIfEmpty(v.building), floor: nullIfEmpty(v.floor) } })}>
      <FieldRow>
        <Field label="Name" htmlFor="rm-name" error={e.name?.message}><Input autoFocus placeholder="e.g. Physics Laboratory" {...form.register("name")} /></Field>
        <Field label="Code" htmlFor="rm-code" error={e.code?.message}><Input className="uppercase" placeholder="LAB-PHY" {...form.register("code")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Campus" htmlFor="rm-campus" error={e.campusId?.message}>
          <NativeSelect disabled={room !== null} {...form.register("campusId")}>{campuses.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</NativeSelect>
        </Field>
        <Field label="Type" htmlFor="rm-type"><NativeSelect {...form.register("type")}>{types.map((t) => <option key={t} value={t}>{roomTypeLabel[t]}</option>)}</NativeSelect></Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Building" htmlFor="rm-building" optional><Input {...form.register("building")} /></Field>
        <Field label="Floor" htmlFor="rm-floor" optional><Input {...form.register("floor")} /></Field>
        <Field label="Capacity" htmlFor="rm-cap" error={e.capacity?.message}><Input type="number" min={0} {...form.register("capacity")} /></Field>
      </FieldRow>
      <Field inline label="Can be booked" hint="Labs, halls and grounds that staff reserve for activities." htmlFor="rm-book">
        <Switch checked={form.watch("isBookable")} onCheckedChange={(v) => form.setValue("isBookable", v, { shouldDirty: true })} />
      </Field>
      <Field inline label="Active" htmlFor="rm-active"><Switch checked={form.watch("isActive")} onCheckedChange={(v) => form.setValue("isActive", v, { shouldDirty: true })} /></Field>
    </FormDialog>
  );
}

export default function RoomsPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage);
  const rooms = useRooms();
  const campuses = useCampuses();
  const [dialog, setDialog] = React.useState<{ open: boolean; room: Room | null }>({ open: false, room: null });

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Rooms" }]} title="Rooms"
        description="Classrooms, laboratories and halls. Timetables use them to avoid double-booking."
        actions={editable && <Button variant="primary" onClick={() => setDialog({ open: true, room: null })}><Plus /> New room</Button>} />
      {rooms.isPending || campuses.isPending ? <Skeleton className="h-96" /> : rooms.error ? <ErrorState error={rooms.error} onRetry={() => void rooms.refetch()} /> :
        rooms.data.length === 0 ? <EmptyState icon={<DoorOpen />} title="No rooms yet" description="Add classrooms and labs so sections and timetables can use them." /> : (
          <div className="grid gap-8">
            {campuses.data?.map((campus) => {
              const here = rooms.data.filter((r) => r.campusId === campus.id);
              if (here.length === 0) return null;
              const seats = here.filter((r) => r.type === "Classroom").reduce((n, r) => n + r.capacity, 0);
              return (
                <section key={campus.id}>
                  <SectionHeading title={campus.name} description={`${pluralize(here.length, "room")} · ${seats} classroom seats`} />
                  <RecordList>
                    {here.map((room) => (
                      <RecordRow key={room.id} muted={!room.isActive} onClick={editable ? () => setDialog({ open: true, room }) : undefined}
                        leading={<span className="w-16 shrink-0 font-mono text-xs text-ink-2">{room.code}</span>}
                        title={room.name}
                        badges={<>{room.isBookable && <Badge tone="accent">Bookable</Badge>}{!room.isActive && <Badge>Inactive</Badge>}</>}
                        subtitle={[roomTypeLabel[room.type], room.building, room.floor && `${room.floor} floor`].filter(Boolean).join(" · ")}
                        facts={[`${room.capacity} seats`]} />
                    ))}
                  </RecordList>
                </section>
              );
            })}
          </div>
        )}
      <RoomDialog open={dialog.open} room={dialog.room} onOpenChange={(o) => setDialog((d) => ({ ...d, open: o }))} />
    </Page>
  );
}
