"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ApiError } from "@/lib/api/client";
import { studentKeys, useUpdateStudent, type StudentDetail } from "@/lib/api/students";
import { toast } from "@/components/ui/toaster";
import { FormDialog } from "@/components/forms/form-dialog";
import {
  FieldGroupHeading,
  StudentBackgroundFields,
  StudentContactFields,
  StudentHealthFields,
  StudentIdentityFields,
  StudentPreviousSchoolFields,
  StudentRegistrationField,
  studentDetailsFrom,
  studentDetailsInput,
  studentDetailsSchema,
  type StudentDetailsValues,
} from "./student-form";

export function EditStudentDialog({ student, open, onOpenChange }: { student: StudentDetail; open: boolean; onOpenChange: (open: boolean) => void }) {
  const update = useUpdateStudent(student.id);
  const queryClient = useQueryClient();
  const form = useForm<StudentDetailsValues>({ resolver: zodResolver(studentDetailsSchema) });
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="xl" defaults={studentDetailsFrom(student)}
      title="Edit student details" description={`${student.fullName} · ${student.admissionNumber}`}
      submitLabel="Save changes" successMessage="Student details saved"
      onSubmit={async (v) => {
        try {
          await update.mutateAsync({ student: studentDetailsInput(v), version: student.version });
        } catch (error) {
          // Someone else saved this record since it was opened: show their version instead of overwriting it.
          if (error instanceof ApiError && error.status === 409) {
            toast.error(error, "Someone else changed this record");
            void queryClient.invalidateQueries({ queryKey: studentKeys.detail(student.id) });
            onOpenChange(false);
            return false;
          }
          throw error;
        }
      }}>
      <FieldGroupHeading>Identity</FieldGroupHeading>
      <StudentIdentityFields form={form} />
      <StudentBackgroundFields form={form} />
      <FieldGroupHeading>Health</FieldGroupHeading>
      <StudentHealthFields form={form} />
      <FieldGroupHeading>Home &amp; contact</FieldGroupHeading>
      <StudentContactFields form={form} />
      <FieldGroupHeading>Schooling</FieldGroupHeading>
      <StudentPreviousSchoolFields form={form} />
      <StudentRegistrationField form={form} />
    </FormDialog>
  );
}
